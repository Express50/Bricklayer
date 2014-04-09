﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bricklayer.Client.Networking;
using TomShane.Neoforce.Controls;
using Cyral.Extensions;

namespace Bricklayer.Client.Interface
{
    /// <summary>
    /// Dialog for creating rooms in the lobby list
    /// </summary>
    public class CreateWorldDialog : Dialog
    {
        //Controls
        private Button createBtn;
        private TextBox txtName, txtDescription;
        private Label lblName, lblDescription;
        private LobbyWindow roomList;

        public CreateWorldDialog(Manager manager, LobbyWindow parent)
            : base(manager)
        {
            roomList = parent;
            //Setup the window
            Text = "Create World";
            TopPanel.Visible = false;
            Resizable = false;
            Width = 250;
            Height = 190;
            Center();

            //Add controls
            lblName = new Label(manager) { Left = 8, Top = 8, Text = "Name:", Width = this.ClientWidth - 16 };
            lblName.Init();
            Add(lblName);

            txtName = new TextBox(manager) { Left = 8, Top = lblName.Bottom + 4, Width = this.ClientWidth - 16 };
            txtName.Init();
            txtName.TextChanged += new TomShane.Neoforce.Controls.EventHandler(delegate(object o, TomShane.Neoforce.Controls.EventArgs e)
            {
                if (txtName.Text.Length > Bricklayer.Client.Networking.Messages.CreateRoomMessage.MaxNameLength)
                    txtName.Text = txtName.Text.Truncate(Bricklayer.Client.Networking.Messages.CreateRoomMessage.MaxNameLength);
            });
            Add(txtName);

            lblDescription = new Label(manager) { Left = 8, Top = txtName.Bottom + 4, Text = "Description:", Width = this.ClientWidth - 16 };
            lblDescription.Init();
            Add(lblDescription);

            txtDescription = new TextBox(manager) { Left = 8, Top = lblDescription.Bottom + 4, Width = this.ClientWidth - 16, Height = 34, Mode = TextBoxMode.Multiline, ScrollBars = ScrollBars.None };
            txtDescription.Init();
            txtDescription.TextChanged += new TomShane.Neoforce.Controls.EventHandler(delegate(object o, TomShane.Neoforce.Controls.EventArgs e)
            {
                if (txtDescription.Text.Length > Bricklayer.Client.Networking.Messages.CreateRoomMessage.MaxDescriptionLength)
                    txtDescription.Text = txtDescription.Text.Truncate(Bricklayer.Client.Networking.Messages.CreateRoomMessage.MaxDescriptionLength);
            });
            Add(txtDescription);

            createBtn = new Button(manager) { Top = 8, Text = "Create" };
            createBtn.Init();
            createBtn.Left = (Width / 2) - (createBtn.Width / 2);
            createBtn.Click += CreateBtn_Click;
            BottomPanel.Add(createBtn);
        }
        /// <summary>
        /// When the create button is clicked
        /// </summary>
        void CreateBtn_Click(object sender, TomShane.Neoforce.Controls.EventArgs e)
        {
            MainWindow.ScreenManager.SwitchScreen(new GameScreen(txtName.Text, txtDescription.Text));
            Close();
        }
    }
}