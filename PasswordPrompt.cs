using System;
using System.Drawing;
using System.Windows.Forms;

namespace FileCompressor
{
    public class PasswordPrompt : Form
    {
        private TextBox txtPassword;
        private Button btnOK;
        private Button btnCancel;

        public string Password => txtPassword.Text;

        public PasswordPrompt()
        {
            this.Text = "🔐 Enter Password (optional)";
            this.Size = new Size(400, 160);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = false;
            this.MaximizeBox = false;

            Label lbl = new Label()
            {
                Text = "Enter a password to secure the archive (leave blank to skip):",
                AutoSize = true,
                Location = new Point(15, 15),
                Font = new Font("Segoe UI", 9F)
            };
            this.Controls.Add(lbl);

            txtPassword = new TextBox()
            {
                Location = new Point(15, 45),
                Width = 350,
                UseSystemPasswordChar = true
            };
            this.Controls.Add(txtPassword);

            btnOK = new Button()
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(190, 80),
                Width = 80
            };
            this.Controls.Add(btnOK);

            btnCancel = new Button()
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(280, 80),
                Width = 80
            };
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }
    }
}
