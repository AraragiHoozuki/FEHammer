using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.VisualTree;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FEHagemu.Behaviors { 
    public class AutoScrollHelper : AvaloniaObject
    {
       
        public static readonly AttachedProperty<bool> IsHighlightedProperty =
            AvaloniaProperty.RegisterAttached<AutoScrollHelper, Control, bool>("IsHighlighted");

        private static readonly AttachedProperty<CancellationTokenSource?> CurrentScrollCtsProperty =
            AvaloniaProperty.RegisterAttached<AutoScrollHelper, Control, CancellationTokenSource?>("CurrentScrollCts");

        public static bool GetIsHighlighted(Control element) => element.GetValue(IsHighlightedProperty);
        public static void SetIsHighlighted(Control element, bool value) => element.SetValue(IsHighlightedProperty, value);

        static AutoScrollHelper()
        {
         
            IsHighlightedProperty.Changed.AddClassHandler<Control>(OnIsHighlightedChanged);
        }


        private static async void OnIsHighlightedChanged(Control sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool isTrue && isTrue)
            {
                var scrollViewer = sender.FindAncestorOfType<ScrollViewer>();
                if (scrollViewer == null)
                {
                    sender.BringIntoView(); // 找不到 ScrollViewer 就直接跳过去
                    return;
                }

                // === 核心逻辑：取消旧任务，开启新任务 ===

                // 1. 获取该 ScrollViewer 上现存的取消令牌
                var oldCts = scrollViewer.GetValue(CurrentScrollCtsProperty);

                // 2. 如果有正在运行的动画，取消它！
                if (oldCts != null)
                {
                    oldCts.Cancel();
                    oldCts.Dispose();
                }

                // 3. 创建新的取消令牌
                var newCts = new CancellationTokenSource();
                scrollViewer.SetValue(CurrentScrollCtsProperty, newCts);

                try
                {
                    // 4. 执行动画，并传入 Token
                    await SmoothScrollTo(scrollViewer, sender, newCts.Token);
                }
                catch (TaskCanceledException)
                {
                    // 如果这个新任务运行一半又被取消了（用户手速极快点了第三个），
                    // 这里会捕获异常，我们要忽略它，不要让程序崩溃。
                }
                finally
                {
                    // 只有当是当前任务结束时才清理，避免清理了后来者的 Token
                    if (scrollViewer.GetValue(CurrentScrollCtsProperty) == newCts)
                    {
                        scrollViewer.SetValue(CurrentScrollCtsProperty, null);
                        newCts.Dispose();
                    }
                }
            }
        }

        private static async Task SmoothScrollTo(ScrollViewer scrollViewer, Control target, CancellationToken token)
        {
            var relativePosition = target.TranslatePoint(new Point(0, 0), scrollViewer);
            if (relativePosition == null) return;

            var currentOffset = scrollViewer.Offset;
            var targetOffset = new Vector(currentOffset.X, currentOffset.Y + relativePosition.Value.Y);

            if (Math.Abs(currentOffset.Y - targetOffset.Y) < 1) return;

            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(300),
                Easing = new CubicEaseOut(),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0.0),
                        Setters = { new Setter(ScrollViewer.OffsetProperty, currentOffset) }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1.0),
                        Setters = { new Setter(ScrollViewer.OffsetProperty, targetOffset) }
                    }
                }
            };

            // 关键：把 token 传给 RunAsync
            // 这样一旦 Token 被 Cancel，动画会立即停止并抛出 TaskCanceledException
            await animation.RunAsync(scrollViewer, token);
        }
    }
}