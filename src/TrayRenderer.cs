using System.Drawing;
using System.Windows.Forms;

namespace LiangWenFengGu
{
    internal class DarkColorTable : ProfessionalColorTable
    {
        private static readonly Color Back = Color.FromArgb(16, 19, 24);
        private static readonly Color Hover = Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF);
        private static readonly Color Border = Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF);

        public override Color ToolStripDropDownBackground { get { return Back; } }
        public override Color MenuBorder { get { return Border; } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
        public override Color MenuItemSelected { get { return Hover; } }
        public override Color MenuItemSelectedGradientBegin { get { return Hover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Hover; } }
        public override Color MenuItemPressedGradientBegin { get { return Hover; } }
        public override Color MenuItemPressedGradientEnd { get { return Hover; } }
        public override Color ImageMarginGradientBegin { get { return Back; } }
        public override Color ImageMarginGradientMiddle { get { return Back; } }
        public override Color ImageMarginGradientEnd { get { return Back; } }
        public override Color SeparatorDark { get { return Border; } }
        public override Color SeparatorLight { get { return Back; } }
        public override Color CheckBackground { get { return Hover; } }
        public override Color CheckSelectedBackground { get { return Hover; } }
    }

    internal class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable())
        {
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Selected
                ? Color.FromArgb(255, 255, 255, 255)
                : Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(0xF2, 16, 19, 24)))
            {
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (Pen pen = new Pen(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)))
            {
                Rectangle r = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
                e.Graphics.DrawRectangle(pen, r);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Rectangle r = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
            if (e.Item.Selected || e.Item.Pressed)
            {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF)))
                {
                    e.Graphics.FillRectangle(brush, r);
                }
            }
            else
            {
                base.OnRenderMenuItemBackground(e);
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (Pen pen = new Pen(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF)))
            {
                int y = e.Item.Height / 2;
                e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
            }
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle b = e.ImageRectangle;
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(0x33, 0x4C, 0x8D, 0xFF)))
            {
                e.Graphics.FillRectangle(brush, b);
            }
            using (Pen pen = new Pen(Color.FromArgb(0xFF, 0x6E, 0xA0, 0xFF), 2f))
            {
                e.Graphics.DrawLines(pen, new Point[]
                {
                    new Point(b.Left + 3, b.Top + b.Height / 2),
                    new Point(b.Left + b.Width / 2 - 1, b.Bottom - 4),
                    new Point(b.Right - 3, b.Top + 3)
                });
            }
        }
    }
}
