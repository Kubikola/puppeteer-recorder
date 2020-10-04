﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Frontend.Forms
{
    public partial class PlayerForm : Form
    {
        private PlayerOptions po;
        public PlayerForm()
        {
            InitializeComponent();
        }

        public PlayerOptions ExportOptions()
        {
            return po;
        }

        public void BindOptions(PlayerOptions p)
        {
            po = p;
            codeGeneratorOptionsPropertyGrid.SelectedObject = po;
        }
    }
}
