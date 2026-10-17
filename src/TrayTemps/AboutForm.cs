using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace TrayTemps;

internal sealed class AboutForm : Form
{
    private const string EmailAddress = "m15550917135@outlook.com";

    public AboutForm(Icon applicationIcon)
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;

        Text = "关于 Fuck the PC";
        Icon = applicationIcon;
        ClientSize = new Size(500, 300);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(15, 23, 42);
        ForeColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = BackColor,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(26, 24, 26, 22),
            Margin = Padding.Empty
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var iconBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            Image = applicationIcon.ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0, 0, 16, 0),
            BackColor = Color.Transparent
        };

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Fuck the PC",
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold),
            ForeColor = Color.FromArgb(248, 250, 252),
            Margin = Padding.Empty
        };

        var subtitleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "CPU / GPU 温度监控",
            TextAlign = ContentAlignment.TopLeft,
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            Margin = Padding.Empty
        };

        var separator = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(51, 65, 85),
            Margin = new Padding(0, 7, 0, 14)
        };

        var noticeLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "此软件最终解释权归李孟达@所有，未经允许禁止转载！",
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(226, 232, 240),
            Margin = Padding.Empty
        };

        var contactPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty
        };

        var contactLabel = new Label
        {
            AutoSize = true,
            Text = "如有问题请联系",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(148, 163, 184),
            Margin = new Padding(0, 4, 5, 0)
        };

        var emailLink = new LinkLabel
        {
            AutoSize = true,
            Text = EmailAddress,
            LinkColor = Color.FromArgb(56, 189, 248),
            ActiveLinkColor = Color.FromArgb(125, 211, 252),
            VisitedLinkColor = Color.FromArgb(56, 189, 248),
            Margin = new Padding(0, 4, 0, 0)
        };
        emailLink.LinkClicked += (_, _) => OpenEmailClient();

        var okButton = new Button
        {
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Text = "确定",
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(14, 165, 233),
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            Size = new Size(94, 34),
            Margin = Padding.Empty,
            UseVisualStyleBackColor = false
        };
        okButton.FlatAppearance.BorderSize = 0;

        contactPanel.Controls.Add(contactLabel);
        contactPanel.Controls.Add(emailLink);

        layout.Controls.Add(iconBox, 0, 0);
        layout.SetRowSpan(iconBox, 2);
        layout.Controls.Add(titleLabel, 1, 0);
        layout.Controls.Add(subtitleLabel, 1, 1);
        layout.Controls.Add(separator, 0, 2);
        layout.SetColumnSpan(separator, 2);
        layout.Controls.Add(noticeLabel, 0, 3);
        layout.SetColumnSpan(noticeLabel, 2);
        layout.Controls.Add(contactPanel, 0, 4);
        layout.SetColumnSpan(contactPanel, 2);
        layout.Controls.Add(okButton, 0, 5);
        layout.SetColumnSpan(okButton, 2);

        Controls.Add(layout);

        AcceptButton = okButton;
        CancelButton = okButton;
    }

    private static void OpenEmailClient()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"mailto:{EmailAddress}",
                UseShellExecute = true
            });
        }
        catch
        {
            Clipboard.SetText(EmailAddress);
            MessageBox.Show(
                $"无法打开邮件客户端，邮箱地址已复制：\n{EmailAddress}",
                "关于 Fuck the PC",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
