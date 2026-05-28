"""
GLB 缩放+旋转工具：
  1. 等比缩放使 Bregma-Lambda 距离 = 目标值
  2. 绕 X 轴旋转 +90°，将 Bregma→Lambda 轴从 +Z 转到 -Y
     (x, y, z) → (x, -z, y)

用法：
    python scale_skull.py <输入.glb> <目标距离_mm> [输出.glb]
"""
import struct
import json
import sys


def read_glb(path):
    with open(path, 'rb') as f:
        data = f.read()

    magic, version, total_len = struct.unpack_from('<III', data, 0)
    if magic != 0x46546C67:
        raise ValueError(f'not valid glTF 2.0 (magic=0x{magic:08X})')
    if version != 2:
        raise ValueError(f'unsupported glTF version: {version}')

    pos = 12
    json_data = None
    bin_data = None

    while pos < len(data):
        chunk_len, chunk_type = struct.unpack_from('<II', data, pos)
        chunk_data = data[pos + 8 : pos + 8 + chunk_len]
        if chunk_type == 0x4E4F534A:
            json_data = chunk_data
        elif chunk_type == 0x004E4942:
            bin_data = chunk_data
        pos = pos + 8 + chunk_len

    return json_data, bytearray(bin_data)


def write_glb(path, json_bytes, bin_bytes):
    json_padded = json_bytes
    while len(json_padded) % 4 != 0:
        json_padded += b'\x20'

    bin_padded = bytes(bin_bytes)
    while len(bin_padded) % 4 != 0:
        bin_padded += b'\x00'

    # 用对齐后的真实长度计算 header 中的 totalLength
    total_len = 12 + 8 + len(json_padded) + 8 + len(bin_padded)
    header = struct.pack('<III', 0x46546C67, 2, total_len)

    json_chunk = struct.pack('<II', len(json_padded), 0x4E4F534A) + json_padded
    bin_chunk  = struct.pack('<II', len(bin_padded), 0x004E4942) + bin_padded

    with open(path, 'wb') as f:
        f.write(header + json_chunk + bin_chunk)


def rotate_x90(x, y, z):
    """绕 X 轴旋转 +90°: (x, y, z) → (x, -z, y)"""
    return (x, -z, y)


def transform_positions_in_bin(bin_data, gltf, scale):
    """缩放 + 旋转 BIN 中所有 POSITION 类型的 vec3 值"""
    accessors = gltf.get('accessors', [])
    buffer_views = gltf.get('bufferViews', [])
    meshes = gltf.get('meshes', [])

    pos_accessor_indices = set()
    for mesh in meshes:
        for prim in mesh.get('primitives', []):
            pos_idx = prim.get('attributes', {}).get('POSITION')
            if pos_idx is not None:
                pos_accessor_indices.add(pos_idx)

    if not pos_accessor_indices:
        print('  WARN: no POSITION accessor found')
        return bin_data

    count_total = 0
    for acc_idx in pos_accessor_indices:
        acc = accessors[acc_idx]
        count = acc['count']
        component_type = acc['componentType']
        bv_idx = acc.get('bufferView')
        if bv_idx is None:
            continue
        bv = buffer_views[bv_idx]
        offset = bv.get('byteOffset', 0)
        acc_offset = acc.get('byteOffset', 0)
        total_offset = offset + acc_offset

        if component_type != 5126:  # FLOAT only
            print(f'  skip accessor[{acc_idx}]: componentType={component_type}')
            continue

        byte_stride = bv.get('byteStride', 12)

        for i in range(count):
            byte_pos = total_offset + i * byte_stride
            x, y, z = struct.unpack_from('<fff', bin_data, byte_pos)
            # step 1: scale
            x *= scale
            y *= scale
            z *= scale
            # step 2: rotate X+90: (x, y, z) → (x, -z, y)
            nx, ny, nz = rotate_x90(x, y, z)
            struct.pack_into('<fff', bin_data, byte_pos, nx, ny, nz)

        count_total += count

    print(f'  BIN transform: {count_total} vertices x{scale:.6f} + rotX90')

    # update accessor min/max
    for acc_idx in pos_accessor_indices:
        acc = accessors[acc_idx]
        if 'min' in acc:
            x, y, z = acc['min'][0] * scale, acc['min'][1] * scale, acc['min'][2] * scale
            acc['min'] = list(rotate_x90(x, y, z))
        if 'max' in acc:
            x, y, z = acc['max'][0] * scale, acc['max'][1] * scale, acc['max'][2] * scale
            acc['max'] = list(rotate_x90(x, y, z))

    return bin_data


def transform_marker_nodes(gltf, scale):
    """缩放 + 旋转 5 个标记点节点的 translation"""
    nodes = gltf.get('nodes', [])
    count = 0

    for node in nodes:
        name = node.get('name', '')
        if not name.startswith('Point_'):
            continue

        t = node.get('translation')
        if t is None:
            t = [0.0, 0.0, 0.0]
            node['translation'] = t

        # scale + rotate X+90
        t[0], t[1], t[2] = rotate_x90(t[0] * scale, t[1] * scale, t[2] * scale)
        count += 1
        print(f'  {name}: [{t[0]:.4f}, {t[1]:.4f}, {t[2]:.4f}]')

    # also rotate non-Point nodes that have translation (e.g. the mesh root if any)
    for node in nodes:
        name = node.get('name', '')
        if name.startswith('Point_'):
            continue
        t = node.get('translation')
        if t is not None:
            t[0], t[1], t[2] = rotate_x90(t[0] * scale, t[1] * scale, t[2] * scale)

    print(f'  JSON marker transform: {count} nodes')


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(1)

    input_path = sys.argv[1]
    target_distance_mm = float(sys.argv[2])
    output_path = sys.argv[3] if len(sys.argv) > 3 else input_path.replace('.glb', f'_{target_distance_mm}mm.glb')

    print(f'Input:  {input_path}')
    print(f'Target Bregma-Lambda: {target_distance_mm} mm')
    print(f'Output: {output_path}')

    # 1. read
    json_raw, bin_data = read_glb(input_path)
    gltf = json.loads(json_raw.decode('utf-8'))

    # 2. compute current distance
    nodes = gltf.get('nodes', [])
    bregma_t = [0.0, 0.0, 0.0]
    lambda_t = None
    for node in nodes:
        name = node.get('name', '')
        if name == 'Point_1_Bregma':
            bt = node.get('translation')
            if bt is not None:
                bregma_t = bt
        elif name == 'Point_2_Lambda':
            lambda_t = node.get('translation')

    if lambda_t is None:
        print('ERROR: Point_2_Lambda not found')
        sys.exit(1)

    dx = lambda_t[0] - bregma_t[0]
    dy = lambda_t[1] - bregma_t[1]
    dz = lambda_t[2] - bregma_t[2]
    current_dist = (dx*dx + dy*dy + dz*dz) ** 0.5

    print(f'\nCurrent Bregma-Lambda: {current_dist:.4f} mm')
    print(f'  Bregma: ({bregma_t[0]:.4f}, {bregma_t[1]:.4f}, {bregma_t[2]:.4f})')
    print(f'  Lambda: ({lambda_t[0]:.4f}, {lambda_t[1]:.4f}, {lambda_t[2]:.4f})')

    scale = target_distance_mm / current_dist
    print(f'Scale factor: {scale:.6f}')

    # 3. transform BIN vertices
    print('\n--- BIN transform ---')
    bin_data = transform_positions_in_bin(bin_data, gltf, scale)

    # 4. transform JSON marker nodes
    print('\n--- JSON marker transform ---')
    transform_marker_nodes(gltf, scale)

    # 5. write
    new_json = json.dumps(gltf, separators=(',', ':'), ensure_ascii=False).encode('utf-8')
    write_glb(output_path, new_json, bytes(bin_data))

    print(f'\n[DONE] -> {output_path}')

    # 6. verify
    print(f'\n--- Verify ---')
    json_v, bin_v = read_glb(output_path)
    gltf_v = json.loads(json_v.decode('utf-8'))
    for node in gltf_v.get('nodes', []):
        name = node.get('name', '')
        if name == 'Point_1_Bregma':
            bt = node.get('translation', [0,0,0])
            print(f'  Bregma:    ({bt[0]:.4f}, {bt[1]:.4f}, {bt[2]:.4f})')
        elif name == 'Point_2_Lambda':
            lt = node.get('translation', [0,0,0])
            dist = (lt[0]**2 + lt[1]**2 + lt[2]**2) ** 0.5
            print(f'  Lambda:    ({lt[0]:.4f}, {lt[1]:.4f}, {lt[2]:.4f})')
            print(f'  Bregma→Lambda: {dist:.4f} mm')
        elif name == 'Point_3_Midpoint':
            t = node.get('translation', [0,0,0])
            print(f'  Midpoint:  ({t[0]:.4f}, {t[1]:.4f}, {t[2]:.4f})')
        elif name == 'Point_4_MidLeft':
            t = node.get('translation', [0,0,0])
            print(f'  MidLeft:   ({t[0]:.4f}, {t[1]:.4f}, {t[2]:.4f})')
        elif name == 'Point_5_MidRight':
            t = node.get('translation', [0,0,0])
            print(f'  MidRight:  ({t[0]:.4f}, {t[1]:.4f}, {t[2]:.4f})')


if __name__ == '__main__':
    main()
