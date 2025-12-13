using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FEHagemu.Controls
{
    public class NineGridBorder : Decorator
    {
        // --- 1. 图片源 ---
        public static readonly StyledProperty<IImage?> SourceProperty =
            AvaloniaProperty.Register<NineGridBorder, IImage?>(nameof(Source));

        public IImage? Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        // --- 2. 九宫格切割设置 ---
        public static readonly StyledProperty<Thickness> NineGridProperty =
            AvaloniaProperty.Register<NineGridBorder, Thickness>(nameof(NineGrid));

        public Thickness NineGrid
        {
            get => GetValue(NineGridProperty);
            set => SetValue(NineGridProperty, value);
        }

        // 【关键修复】删除这里的 PaddingProperty 定义！
        // Decorator 基类已经有 Padding 了，直接用基类的。

        static NineGridBorder()
        {
            AffectsRender<NineGridBorder>(SourceProperty, NineGridProperty);
            // Padding 的变化基类会自动处理 Measure 请求，但我们需要确保重绘（如果布局改变影响渲染的话）
            // 通常 Decorator 已经处理了 Padding 的 AffectsMeasure
        }

        public override void Render(DrawingContext context)
        {
            var source = Source;
            if (source == null) return;

            var bounds = new Rect(Bounds.Size);
            var grid = NineGrid;
            // IImage 也有 Size，注意不要用 Source.Size 造成空引用
            var srcSize = source.Size;

            if (grid == new Thickness(0) || srcSize.Width <= 0 || srcSize.Height <= 0)
            {
                context.DrawImage(source, new Rect(srcSize), bounds);
                return;
            }

            // ... 保持你的绘制逻辑不变 ...
            double[] srcX = { 0, grid.Left, srcSize.Width - grid.Right, srcSize.Width };
            double[] srcY = { 0, grid.Top, srcSize.Height - grid.Bottom, srcSize.Height };
            double[] destX = { 0, grid.Left, bounds.Width - grid.Right, bounds.Width };
            double[] destY = { 0, grid.Top, bounds.Height - grid.Bottom, bounds.Height };

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    var sRect = new Rect(srcX[i], srcY[j], Math.Max(0, srcX[i + 1] - srcX[i]), Math.Max(0, srcY[j + 1] - srcY[j]));
                    var dRect = new Rect(destX[i], destY[j], Math.Max(0, destX[i + 1] - destX[i]), Math.Max(0, destY[j + 1] - destY[j]));

                    if (sRect.Width > 0 && sRect.Height > 0 && dRect.Width > 0 && dRect.Height > 0)
                    {
                        context.DrawImage(source, sRect, dRect);
                    }
                }
            }
            //Test
            //context.DrawRectangle(null, new Pen(Brushes.Red, 2), bounds);
        }

        //protected override Size MeasureOverride(Size availableSize)
        //{
        //    // 直接使用基类的 Padding
        //    var padding = Padding;

        //    var availableSpaceForChild = availableSize.Deflate(padding);
        //    Child?.Measure(availableSpaceForChild);

        //    var childSize = Child?.DesiredSize ?? new Size(0, 0);
        //    var totalSize = childSize.Inflate(padding);

        //    var minBgW = NineGrid.Left + NineGrid.Right;
        //    var minBgH = NineGrid.Top + NineGrid.Bottom;

        //    return new Size(
        //        Math.Max(totalSize.Width, minBgW),
        //        Math.Max(totalSize.Height, minBgH)
        //    );
        //}

        //protected override Size ArrangeOverride(Size finalSize)
        //{
        //    var padding = Padding;
        //    var childRect = new Rect(finalSize).Deflate(padding);
        //    Child?.Arrange(childRect);
        //    return finalSize;
        //}
    }
}
