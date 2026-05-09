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
        private JudgeEventArgs judgeEventArgs;
        private bool Judge(string username, string passport)
        {
            bool result = true;

            judgeEventArgs = new JudgeEventArgs(result);
            raiseJudge(judgeEventArgs);
            return result;
        }

        #region Events
        public class JudgeEventArgs : EventArgs
        {
            private bool result;
            public bool Result
            {
                get { return result; }
            }
            public JudgeEventArgs(bool result)
            {
                this.result = result;
            }
        }

        public delegate void JudgeEventHandler(object sender, JudgeEventArgs e);
        public event JudgeEventHandler onJudged;
        private void raiseJudge(JudgeEventArgs e)
        {
            if (onJudged != null)
                onJudged(this, e);
        }
        #endregion
    }
}
