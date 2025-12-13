using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FEHagemu.Controls
{
    /// <summary>
    /// 实现类似 Unity/WPF 的 9-Slice (九宫格) 图片拉伸控件
    /// </summary>
    public class NineGridImage : Control
    {
        // 1. 将 IBitmap 修改为 IImage
        // IImage 是 Avalonia 中所有图像资源的基接口
        public static readonly StyledProperty<IImage?> SourceProperty =
            AvaloniaProperty.Register<NineGridImage, IImage?>(nameof(Source));

        public IImage? Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        // 2. NineGrid 属性保持不变
        public static readonly StyledProperty<Thickness> NineGridProperty =
            AvaloniaProperty.Register<NineGridImage, Thickness>(nameof(NineGrid));

        public Thickness NineGrid
        {
            get => GetValue(NineGridProperty);
            set => SetValue(NineGridProperty, value);
        }

        static NineGridImage()
        {
            AffectsRender<NineGridImage>(SourceProperty, NineGridProperty);
            AffectsMeasure<NineGridImage>(SourceProperty, NineGridProperty);
        }

        public override void Render(DrawingContext context)
        {
            var source = Source;
            if (source == null) return;

            var bounds = new Rect(Bounds.Size);
            var grid = NineGrid;
            var srcSize = source.Size; // IImage 也有 Size 属性

            // 如果没有设置 NineGrid 或图片无效，直接普通拉伸绘制
            if (grid == new Thickness(0) || srcSize.Width <= 0 || srcSize.Height <= 0)
            {
                context.DrawImage(source, new Rect(srcSize), bounds);
                return;
            }

            // --- 核心计算逻辑 (同前) ---

            double[] srcX = { 0, grid.Left, srcSize.Width - grid.Right, srcSize.Width };
            double[] srcY = { 0, grid.Top, srcSize.Height - grid.Bottom, srcSize.Height };

            double[] destX = { 0, grid.Left, bounds.Width - grid.Right, bounds.Width };
            double[] destY = { 0, grid.Top, bounds.Height - grid.Bottom, bounds.Height };

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    var sRect = new Rect(
                        srcX[i],
                        srcY[j],
                        Math.Max(0, srcX[i + 1] - srcX[i]),
                        Math.Max(0, srcY[j + 1] - srcY[j]));

                    var dRect = new Rect(
                        destX[i],
                        destY[j],
                        Math.Max(0, destX[i + 1] - destX[i]),
                        Math.Max(0, destY[j + 1] - destY[j]));

                    if (sRect.Width > 0 && sRect.Height > 0 && dRect.Width > 0 && dRect.Height > 0)
                    {
                        // DrawImage 接受 IImage，所以这里可以直接传 source
                        context.DrawImage(source, sRect, dRect);
                    }
                }
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var source = Source;
            if (source == null) return new Size(0, 0);

            // 最小尺寸为九宫格的固定边缘大小
            return new Size(NineGrid.Left + NineGrid.Right, NineGrid.Top + NineGrid.Bottom);
        }
    }
}
