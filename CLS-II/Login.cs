using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CLS_II
{
    public partial class Login : Form
    {
        public Login()
        {
            InitializeComponent();
        }

        private void Login_Load(object sender, EventArgs e)
        {
            MultiLanguage.LoadLanguage(this, typeof(Login));
        }

        private void button_Cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button_Confirm_Click(object sender, EventArgs e)
        {
            if (!Judge(textBox1.Text, textBox2.Text))
            {
                if (MultiLanguage.DefaultLanguage != "zh")
                    MessageBox.Show("Error username or passport", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                else
                    MessageBox.Show("用户名或密码错误", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                if (MultiLanguage.DefaultLanguage != "zh")
                    MessageBox.Show("Login successful", "Administrator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    MessageBox.Show("登陆成功", "管理员", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
                textBox2.Focus();
        }

        private void textBox2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
                button_Confirm.Focus();
        }
    }
}
