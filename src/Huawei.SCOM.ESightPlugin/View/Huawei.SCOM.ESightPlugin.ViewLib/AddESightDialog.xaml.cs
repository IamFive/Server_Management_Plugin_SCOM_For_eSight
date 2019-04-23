using Huawei.SCOM.ESightPlugin.ViewLib.Model;
using Huawei.SCOM.ESightPlugin.ViewLib.Repo;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Huawei.SCOM.ESightPlugin.ViewLib
{
    /// <summary>
    /// AddESightDialog.xaml 的交互逻辑
    /// </summary>
    public partial class AddESightDialog : Window, INotifyPropertyChanged
    {
        private ESightApplianceRepo ESightApplianceRepo { get; }
        private ESightAppliance _item = new ESightAppliance();
        private Result _actionResult = Result.Done();

        public ESightAppliance Item
        {
            get => _item;
            set
            {
                if (_item != value)
                {
                    _item = value;
                    this.OnPropertyChanged("ActionResult");
                }
            }
        }
        public Result ActionResult
        {
            get => _actionResult;

            set
            {

                if (_actionResult != value)
                {
                    _actionResult = value;
                    this.OnPropertyChanged("ActionResult");
                }
            }
        }

        public AddESightDialog(ESightApplianceRepo repo)
        {
            InitializeComponent();
            this.ESightApplianceRepo = repo;
            this.ShowInTaskbar = false;
            this.DataContext = this;
        }


        private void OnSaveBtnClicked(object sender, RoutedEventArgs e)
        {
            this.ActionResult = Result.Done();
            ESightAppliance eSight = new ESightAppliance
            {
                Host = txtHost.Text,
                Port = txtPort.Text,
                SystemId = txtSystemId.Text,
                AliasName = txtAlias.Text,
                LoginAccount = txtAccount.Text,
                LoginPassword = txtPassword.Password,
                UpdateCredential = true,
            };

            ESightApplianceRepo.Add(eSight, AddEsightCallback);
        }
        void AddEsightCallback(Result result)
        {
            this.ActionResult = result;
            if (result.Success)
            {
                this.Close();
            }
        }

        private void OnTestBtnClicked(object sender, RoutedEventArgs e)
        {
            this.ActionResult = Result.Done();
            ESightAppliance eSight = new ESightAppliance
            {
                Host = txtHost.Text,
                Port = txtPort.Text,
                SystemId = txtSystemId.Text,
                AliasName = txtAlias.Text,
                LoginAccount = txtAccount.Text,
                LoginPassword = txtPassword.Password,
                UpdateCredential = true,
            };

            ESightApplianceRepo.Test(eSight, TestEsightCallback);
        }

        void TestEsightCallback(Result result)
        {
            this.ActionResult = result;
        }

        private void OnCloseBtnClicked(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                this.DragMove();
            }
            catch { }
        }

        private void OnCancelBtnClicked(object sender, RoutedEventArgs e)
        {
            this.Close();
        }


        #region NotifyPropertyChanged 
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion //NotifyPropertyChanged 
    }
}
