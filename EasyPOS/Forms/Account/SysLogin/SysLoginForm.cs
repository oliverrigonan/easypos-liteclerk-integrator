﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyPOS.Forms.Account.SysLogin
{
    public partial class SysLoginForm : Form
    {
        public SysLoginForm()
        {
            InitializeComponent();
            dateTimePickerLoginDate.Enabled = false;

            textBoxUsername.Focus();
        }

        private void buttonClose_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void buttonLogin_Click(object sender, EventArgs e)
        {
            Login();
        }

        public void Login()
        {
            Controllers.SysLoginController sysLoginController = new Controllers.SysLoginController();

            String[] login = sysLoginController.Login(textBoxUsername.Text, textBoxPassword.Text, dateTimePickerLoginDate.Value.ToShortDateString(), radioButtonLoginDate.Checked);
            if (login[1].Equals("0") == false)
            {
                Hide();

                Software.SysSoftwareForm sysSoftwareForm = new Software.SysSoftwareForm();
                sysSoftwareForm.Show();
            }
            else
            {
                MessageBox.Show(login[0], "Easy POS", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void textBoxUsername_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                Login();
            }
        }

        private void textBoxPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                Login();
            }
        }

        private void SysLoginForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Environment.Exit(0);
        }

        private void radioButtonSystemDate_CheckedChanged(object sender, EventArgs e)
        {
            dateTimePickerLoginDate.Enabled = false;
        }

        private void radioButtonLoginDate_CheckedChanged(object sender, EventArgs e)
        {
            dateTimePickerLoginDate.Enabled = true;
        }
    }
}
