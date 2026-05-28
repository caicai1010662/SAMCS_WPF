using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SAMCS_WPF.ViewModels
{
    /// <summary>
    /// 颅骨配准窗口 ViewModel —— 展示五点采集结果与坐标系配准数据。
    /// 由 StereotaxicViewModel 在五点采集完成后传递数据创建。
    /// </summary>
    public partial class RegistrationViewModel : ObservableObject
    {
        // ===================== 调平状态 =====================

        [ObservableProperty]
        private bool _isSkullLeveled;

        [ObservableProperty]
        private string _bregmaLambdaDvInfo = "";

        [ObservableProperty]
        private string _midLeftRightDvInfo = "";

        [ObservableProperty]
        private string _bregmaLambdaDistance = "";

        // ===================== 五点 AP/ML/DV 坐标 =====================

        [ObservableProperty]
        private string _bregmaAp = "0.000";

        [ObservableProperty]
        private string _bregmaMl = "0.000";

        [ObservableProperty]
        private string _bregmaDv = "0.000";

        [ObservableProperty]
        private string _lambdaAp = "";

        [ObservableProperty]
        private string _lambdaMl = "0.000";

        [ObservableProperty]
        private string _lambdaDv = "0.000";

        [ObservableProperty]
        private string _midpointAp = "";

        [ObservableProperty]
        private string _midpointMl = "0.000";

        [ObservableProperty]
        private string _midpointDv = "";

        [ObservableProperty]
        private string _midLeftAp = "";

        [ObservableProperty]
        private string _midLeftMl = "";

        [ObservableProperty]
        private string _midLeftDv = "";

        [ObservableProperty]
        private string _midRightAp = "";

        [ObservableProperty]
        private string _midRightMl = "";

        [ObservableProperty]
        private string _midRightDv = "";

        // ===================== 构造函数 =====================

        /// <summary>
        /// 从五点六轴位置快照计算 AP/ML/DV 坐标系配准数据。
        ///
        /// 坐标系定义：
        ///   Bregma = 原点 (AP=0, ML=0, DV=0)
        ///   AP 轴 = Bregma→Lambda 方向（硬件 Y 轴），前向为正
        ///   ML 轴 = 硬件 X 轴，右向为正
        ///   DV 轴 = 硬件 Z 轴，上向为正
        ///
        /// 位置数组按 UI 顺序：R=0, P=1, Y=2, X=3, Z=4, I=5
        /// </summary>
        public RegistrationViewModel(
            bool isSkullLeveled,
            string bregmaLambdaDvInfo,
            string midLeftRightDvInfo,
            float[]? bregma,
            float[]? lambda,
            float[]? midpoint,
            float[]? midLeft,
            float[]? midRight)
        {
            IsSkullLeveled = isSkullLeveled;
            BregmaLambdaDvInfo = bregmaLambdaDvInfo;
            MidLeftRightDvInfo = midLeftRightDvInfo;

            // 前囟为原点
            BregmaAp = "0.000";
            BregmaMl = "0.000";
            BregmaDv = "0.000";

            if (bregma != null && lambda != null)
            {
                // Bregma→Lambda 在硬件 Y 轴上的距离（mm）
                float dy = lambda[2] - bregma[2];
                float dx = lambda[3] - bregma[3];
                float dz = lambda[4] - bregma[4];
                float distance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

                BregmaLambdaDistance = string.Format("{0:F3} mm", distance);

                // Lambda: AP = -d (Bregma→Lambda = AP 负方向)
                LambdaAp = string.Format("{0:F3}", -distance);
                LambdaMl = "0.000";
                LambdaDv = string.Format("{0:F3}", lambda[4] - bregma[4]);
            }

            if (bregma != null && midpoint != null)
            {
                // 中点 AP = -d/2
                float midAp = midpoint[2] - bregma[2];
                MidpointAp = string.Format("{0:F3}", -Math.Abs(midAp));
                MidpointMl = "0.000";
                MidpointDv = string.Format("{0:F3}", midpoint[4] - bregma[4]);
            }

            if (bregma != null && midLeft != null)
            {
                float ap = midLeft[2] - bregma[2];
                float ml = midLeft[3] - (midpoint != null ? midpoint[3] : bregma[3]);
                MidLeftAp = string.Format("{0:F3}", -Math.Abs(ap));
                MidLeftMl = string.Format("{0:F3}", ml);
                MidLeftDv = string.Format("{0:F3}", midLeft[4] - bregma[4]);
            }

            if (bregma != null && midRight != null)
            {
                float ap = midRight[2] - bregma[2];
                float ml = midRight[3] - (midpoint != null ? midpoint[3] : bregma[3]);
                MidRightAp = string.Format("{0:F3}", -Math.Abs(ap));
                MidRightMl = string.Format("{0:F3}", ml);
                MidRightDv = string.Format("{0:F3}", midRight[4] - bregma[4]);
            }
        }

        // ===================== 命令 =====================

        [RelayCommand]
        private void Close()
        {
            // 由 Window 的 Window.Closing 或按钮 Command 触发关闭
        }
    }
}
