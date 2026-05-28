# CLAUDE.md

## 项目概述

- **项目类型**: WPF 上位机，MVVM 架构
- **应用领域**: 多自由度高精度脑立体定向微电极植入机器人
- **通信方式**: 串口通信
- **控制类型**: 电机运动控制
- **界面风格**: 工业控制风格

### 三大功能页面

| 页面 | 进度 | 技术栈 |
|------|------|--------|
| 状态监控（MonitorView） | ✅ 已完成 | WebView2 + Three.js + URDFLoader + STL |
| 脑立体定向（StereotaxicView） | 🚧 开发中 | WebView2 + Three.js + GLTFLoader + GLB |
| 微电极植入（ImplantView） | ⏳ 预留 | 待定 |

### 项目文件结构（关键路径）

```
SAMCS_WPF/
├── Views/
│   ├── MonitorView.xaml / .cs       ← 状态监控（六轴机器人 3D + 参数表）
│   ├── StereotaxicView.xaml / .cs   ← 脑立体定向（颅骨 3D + 功能面板）
│   ├── ImplantView.xaml / .cs       ← 微电极植入（预留）
│   └── MainWindow.xaml              ← 导航栏 + 三页面容器
├── ViewModels/
│   ├── MonitorViewModel.cs
│   ├── StereotaxicViewModel.cs
│   ├── ImplantViewModel.cs
│   └── MainViewModel.cs
├── Services/
│   ├── IRobotControlService.cs      ← 串口通信接口
│   ├── RobotControlService.cs
│   ├── IAxisWorkflowService.cs      ← 运动流程编排接口
│   └── AxisWorkflowService.cs
├── 3D/
│   ├── Runtime/
│   │   ├── index.html               ← MonitorView 的 URDF 机器人页面
│   │   ├── SkullViewer.html         ← StereotaxicView 的 GLB 颅骨页面
│   │   ├── js/                      ← Three.js 核心 + 加载器
│   │   └── utils/                   ← BufferGeometryUtils.js
│   └── Model/
│       ├── SAMCS.urdf               ← 六轴机器人结构
│       ├── MouseSkull.glb           ← 小鼠颅骨模型（含 5 个标记点）
│       └── meshes/                  ← 9 个 STL 零件网格
└── assets/
    └── MouseSkull.glb               ← 颅骨源文件（编译时 Link 到 3D/Model/）
```

## 用户背景

用户仅具备少量 C 与 C# 基础，代码可读性优先于高级设计。

## 交互规则

1. **先解释再改代码**: 修改代码前，必须先用语言解释：
   - 当前代码在干什么
   - 为什么这样写
   - 存在什么问题
   - 准备怎么改
2. **等用户确认**: 解释完毕后，等用户理解并确认，再输出实际代码修改
3. **风格优先级**: 永远优先"工业控制程序员风格"，而不是"互联网高级架构师风格"

## 代码风格规则

1. **可读性优先**: 代码可读性 > 代码优雅性
2. **不过度封装**: 保持代码扁平，避免不必要的抽象层
3. **不复用强迫**: 不要为了复用强行抽象通用函数
4. **禁止复杂设计模式**: 不使用工厂、策略、观察者等复杂模式
5. **禁止高级语法糖**: 不使用 ??=、?.、switch 表达式、模式匹配等语法糖
6. **优先传统 for 循环**: 尽量使用 `for (int i = 0; i < n; i++)` 而非 foreach
7. **避免 LINQ**: 不使用 `.Where()`, `.Select()`, `.ToList()` 等 LINQ 方法
8. **禁止高级特性**: 不使用复杂委托、表达式树、反射、dynamic
9. **避免多层嵌套**: 保持代码扁平，if/for 嵌套不超过 2-3 层
10. **不隐藏关键逻辑**: 核心流程直接展开，不隐藏在辅助函数中
11. **可调试性**: 所有代码必须可以 F10/F11 单步调试
12. **中文注释**: 每一步关键逻辑必须写中文注释，说明 WHY
13. **运动流程展开**: 运动控制流程全部展开写，不抽象子函数
14. **工业控制风格**: 优先工业控制代码风格，而非互联网架构风格
15. **运动控制四重点**: 安全性、可调试性、状态可观察性、机械行为可预测性
16. **运动步骤注释**: 每一步运动、每一次等待、每一次状态判断，必须注释其目的
17. **async/await 注释**: 使用异步时必须注释：为什么异步、为什么等待、为什么不会阻塞 UI
18. **修改现有函数**: 先解释原逻辑 → 再解释修改后逻辑 → 最后说明修改原因
19. **主动发现风险**: 如代码存在时序问题、状态问题、多线程问题、运动安全风险，必须明确指出

## 教学规则

1. 既是代码生成器，也是技术导师
2. 主动解释: 变量作用、函数作用、执行流程、数据流向、状态变化
3. 用户不理解时，用"接近 C 语言思维"的方式解释
4. 不默认用户理解 MVVM、WPF Binding、异步、RelayCommand、ObservableProperty 等高级概念
5. 逐步建立用户的工程理解能力

## 代码输出规则

1. 代码完整展开，不省略中间步骤
2. 一次只修改一个主要逻辑点
3. 优先让用户能看懂、Debug、维护，而非追求"看起来高级"

## 3D 监控界面开发铁律（URDF/STL 机器人模型）

以下每一条都是踩坑之后总结出来的，违反就会出 bug。
适用于 MonitorView 的六轴机器人 3D 渲染。

### URDF 模型颜色

1. **颜色必须写在 `.urdf` 文件的 `<color>` 标签里**，不要在 JS 里通过 `robot.traverse` 去改 material。
   - 原因：STL 文件是异步加载的，`URDFLoader.load()` 的回调触发时，mesh 还没创建完毕，遍历找不到东西。
   - URDFLoader 解析 XML 时**同步读取** `<color>` 值，所以只有改 URDF 才能确保颜色生效。
2. RGBA 格式：`<color rgba="R G B A" />`，每个值 0~1，不是 0~255。

### WebView2 透明与圆角

3. WPF 侧设 `DefaultBackgroundColor="Transparent"` 可以让 WebView2 背景透明。
4. **透明背景 + 圆角不能同时生效**：Chromium 合成器在透明模式下不裁剪根层。
   - 解决方案：`border-radius` 必须写在 HTML 内部的 `#viewport` div 上，并给该 div 设不透明背景色。

### Three.js 渲染审美

5. **布光只用 3 盏灯**：AmbientLight(0.4) + DirectionalLight(2.0) 做主光 + DirectionalLight(1.0) 做轮廓光。5 盏灯会冲淡颜色层次。
6. **不使用色调映射**（ACESFilmicToneMapping）：会让画面发灰发闷，工业用干净直出即可。
7. **天空用 scene.background 设亮蓝色**（如 `0xBADDFC`），搭配 `scene.fog` 做远处雾化。
8. **地面用 CircleGeometry 圆形深色地面**，不要用 PlaneGeometry 方形地面。`groundPlane.position.y` 可调，默认 -0.12。
9. **STL 表面不适用 EdgesGeometry**：STL 模型曲面光滑、三角面密度高、硬边少，即使用 1° 阈值也看不见线框，不要用。

### Three.js 技术细节

10. **阴影必须在模型加载完成后设置**：用 `THREE.LoadingManager` 的 `onLoad` 回调，遍历 `scene` 给所有 `child.isMesh` 设 `castShadow = true; receiveShadow = true`。
11. **JavaScript 颜色格式**：`new THREE.Color(0xRRGGBB)`，不是 CSS 的 `#RRGGBB`。
12. **加载管理器顺序**：`new URDFLoader(manager)` 把 manager 传入构造器，`manager.onLoad` 会在**所有** STL 异步加载完成后触发。

### 关节方向修正

13. 3D 模型中如果某关节旋转方向与实物相反，在 `index.html` 里建 `jointDirectionMap`（如 `{ 'joint2': -1 }`），驱动时乘以方向系数。不要试图改 URDF。

### WPF 模板触发器

14. **ControlTemplate.Triggers 里不能通过 x:Name 找元素**（如 `<Border.RenderTransform><ScaleTransform x:Name="scale"/></Border.RenderTransform>`），会报 MC4111 错误。属性元素语法下的 x:Name 不在模板命名域内。改用 Background 颜色变化代替 ScaleTransform 动画。

---

## GLB 颅骨模型开发铁律（WebView2 + Three.js + GLTFLoader）

从脑立体定向界面（StereotaxicView）的 GLB 颅骨渲染中总结出来的坑。

### GLTFLoader 加载链路

1. **GLTFLoader 有子依赖**：下载 `GLTFLoader.js` 时必须同时下载 `BufferGeometryUtils.js`，且必须放在 `utils/` 目录（相对路径 `../utils/BufferGeometryUtils.js`）。缺这个文件会导致 JS 模块加载失败、模型不显示。
2. **GLB 用 GLTFLoader，URDF 用 URDFLoader**：两种加载机制完全不同，不能混用，必须各写一个独立 HTML。

### GLB 材质与颜色

3. **JS 布尔值全小写**：`true` / `false`，大写 `True` / `False` 是 Python 写法，JS 中会 ReferenceError 导致整个模块崩溃。
4. **material.color 赋值必须 `new THREE.Color(0xRRGGBB)`**，裸数字无效。
5. **GLB 材质如果启用了顶点色**（`material.vertexColors`），直接设 `.color` 不会生效。必须先 `material.vertexColors = false`。
6. **骨骼 PBR 材质标准方案**：骨白色 `0xe6e2d6`（象牙暖白），粗糙度 `0.65`，金属度 `0.0`，`side: THREE.DoubleSide`。
7. **半球光比环境光更逼真**：`HemisphereLight(0xffffff, 0xebd9d3, 0.5)` — 天空冷白 + 地面浅肉色，暗部模拟组织漫反射。

### GLB 模型位置与缩放

8. **自动包围盒三步处理**：加载后自动算 `Box3` → 等比缩放 → XZ 居中 + Y 贴地。
9. **贴地公式**（Three.js 先缩放后平移）：`position.y = GROUND_Y - scale * center.y + scale * size.y / 2`。`GROUND_Y` 必须和圆形地面的 `position.y` 一致。
10. **GLB 标记点（空节点）可视化**：GLB 中的无 mesh 节点是纯坐标标记。用 `node.getWorldPosition()` 获取世界坐标，创建 `SphereGeometry` 球体挂在场景根节点下（不挂在颅骨下，避免被缩放拉伸）。

### 调试工作流

11. **Debug 模式热重载**：`#if DEBUG` 下虚拟主机直指源码目录，改 HTML/JS 后点击右侧面板按 F5 刷新 WebView2，无需重启程序、无需编译 C#。
12. **JS 控制台中继**：劫持 `console.log/error/warn`，通过 `window.chrome.webview.postMessage()` 发到 C# `Debug.WriteLine`，在 VS 输出窗口直接看到 JS 错误。
13. **启动默认页面**：改 `MainViewModel.cs` 的 `_selectedPage` 初始值（`"Monitor"` / `"Stereotaxic"` / `"Implant"`）。

### GLB 文件结构读取

14. **GLB 是 glTF 2.0 二进制容器**：用 Python 读取前 12 字节 header（magic + version + length），再读 JSON chunk（chunkLength + chunkType + data），`json.loads()` 后遍历 `nodes` / `meshes` 数组可提取所有标记点名称和坐标。

---

## 项目方向

这是工业控制、运动控制、脑立体定向、微电极植入方向的上位机。代码优先考虑:

- **稳定性** > 架构炫技
- **安全性** > 代码简洁
- **机械行为可预测性** > 设计模式
