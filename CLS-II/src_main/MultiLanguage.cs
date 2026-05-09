using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CLS_II
{
    //用于编写与切换语言相关的变量和代码
    class MultiLanguage
    {
        //当前默认语言
        public static string DefaultLanguage = "zh";

        /// <summary>
        /// 修改默认语言
        /// </summary>
        /// <param name="lang">待设置默认语言</param>
        public static void SetDefaultLanguage(string lang)
        {
            System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(lang);
            DefaultLanguage = lang;
            Properties.Settings.Default.DefaultLanguage = lang;
            Properties.Settings.Default.Save();
        }


        /// <summary>
        /// 加载语言
        /// </summary>
        /// <param name="form">加载语言的窗口</param>
        /// <param name="formType">窗口的类型</param>
        public static void LoadLanguage(Form form, Type formType)
        {
            if (form != null)
            {
                System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(formType);
                resources.ApplyResources(form, "$this");
                Loading(form, resources);
            }
        }

        /// <summary>
        /// 加载语言
        /// </summary>
        /// <param name="control">控件</param>
        /// <param name="resources">语言资源</param>
        private static void Loading(Control control, System.ComponentModel.ComponentResourceManager resources)
        {
            if (control is MenuStrip)
            {
                //将资源与控件对应
                resources.ApplyResources(control, control.Name);
                MenuStrip ms = (MenuStrip)control;
                if (ms.Items.Count > 0)
                {
                    foreach (ToolStripMenuItem c in ms.Items)
                    {
                        //遍历菜单
                        Loading(c, resources);
                    }
                }
            }

            if (control is StatusStrip)
            {
                StatusStrip ss = (StatusStrip)control;
                foreach (ToolStripStatusLabel s in ss.Items)
                {
                    resources.ApplyResources(s, s.Name);
                }
            }

            if (control is ToolStrip)
            {
                ToolStrip ts = (ToolStrip)control;
                foreach (object t in ts.Items)
                {
                    if (t is ToolStripButton)
                        resources.ApplyResources(t, ((ToolStripButton)t).Name);
                    else if (t is ToolStripLabel)
                        resources.ApplyResources(t, ((ToolStripLabel)t).Name);
                    else if (t is ToolStripDropDownButton)
                    {
                        ToolStripDropDownButton tb = (ToolStripDropDownButton)t;
                        resources.ApplyResources(t, ((ToolStripDropDownButton)t).Name);
                        foreach (object ti in tb.DropDownItems)
                        {
                            resources.ApplyResources(ti, ((ToolStripMenuItem)ti).Name);
                        }
                    }
                    //    resources.ApplyResources(t, ((ToolStripDropDownButton)t).Name);
                    //else if (t is ToolStripMenuItem)
                    //    resources.ApplyResources(t, ((ToolStripMenuItem)t).Name);
                }
            }

            if (control is DataGridView)
            {
                DataGridView dv = (DataGridView)control;
                foreach (object d in dv.Columns)
                {
                    resources.ApplyResources(d, ((DataGridViewColumn)d).Name);
                }
            }

            if (control is TreeView)
            {
                TreeView tv = (TreeView)control;
                resources.ApplyResources(tv, tv.Name);
                foreach (object d in tv.Nodes)
                {
                    resources.ApplyResources(d, ((TreeNode)d).Name);
                }
            }

            foreach (Control c in control.Controls)
            {
                resources.ApplyResources(c, c.Name);
                Loading(c, resources);
            }
        }


        /// <summary>
        /// 遍历菜单
        /// </summary>
        /// <param name="item">菜单项</param>
        /// <param name="resources">语言资源</param>
        private static void Loading(ToolStripMenuItem item, System.ComponentModel.ComponentResourceManager resources)
        {
            if (item is ToolStripMenuItem)
            {
                resources.ApplyResources(item, item.Name);
                ToolStripMenuItem tsmi = (ToolStripMenuItem)item;
                if (tsmi.DropDownItems.Count > 0)
                {
                    foreach (object c in tsmi.DropDownItems)
                    {
                        if (c is ToolStripMenuItem)
                            Loading((ToolStripMenuItem)c, resources);
                    }
                }
            }
        }
    }
}


