using UnityEngine;

namespace CUC260905.Feedback
{
    /// <summary>
    /// 一次圆形背景反馈的完整参数：位置、半径、颜色与存在时长。
    /// 位置为世界坐标（Vector3 可隐式转换）；半径与游戏世界单位一致。
    /// </summary>
    public readonly struct CircleFeedbackRequest
    {
        public readonly Vector2 Position;
        public readonly float Radius;
        public readonly Color Color;
        public readonly float Duration;

        public CircleFeedbackRequest(Vector2 position, float radius, Color color, float duration)
        {
            Position = position;
            Radius = radius;
            Color = color;
            Duration = duration;
        }
    }
}
