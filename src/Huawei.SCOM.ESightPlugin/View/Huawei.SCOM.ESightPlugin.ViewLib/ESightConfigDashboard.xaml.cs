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
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel.Composition;
using Huawei.SCOM.ESightPlugin.ViewLib.Repo;
using Huawei.SCOM.ESightPlugin.ViewLib.Model;

namespace Huawei.SCOM.ESightPlugin.ViewLib
{

    [Export]
    [PartCreationPolicy(CreationPolicy.NonShared)]

    public partial class ESightConfigDashboard : UserControl, INotifyPropertyChanged
    {
        private Result _actionResult = Result.Done();
        private ESightApplianceRepo _eSightApplianceRepo;


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

        public ESightApplianceRepo ESightApplianceRepo { get => _eSightApplianceRepo; set => _eSightApplianceRepo = value; }

        public ESightConfigDashboard()
        {
            InitializeComponent();
            this.ESightApplianceRepo = this.Resources["ESightApplianceRepo"] as ESightApplianceRepo;
            this.ESightApplianceRepo.LoadAll(LoadESightAppliancesCallback);
        }

        void LoadESightAppliancesCallback(Result result)
        {
            this.ActionResult = result;
            if (result.Success)
            {

            }
            else
            {
                // TODO show error dialog?
            }
        }


        public void OnSearchESight()
        {
            string keyword = txtSearchKeyword.Text;
            this.ESightApplianceRepo.Filter(keyword);
            // this.UpdateGridItemSource();
        }

        private void OnGridLoaded(object sender, RoutedEventArgs e)
        {
            // ... Assign ItemsSource of DataGrid.
            // grid = sender as DataGrid;
            // OnSearchESight();
        }


        private void OnDeleteESight(object sender, RoutedEventArgs e)
        {
            int selectedIndex = Grid.SelectedIndex;
            if (Grid.SelectedIndex > -1 && selectedIndex < this.ESightApplianceRepo.FilteredItems.Count)
            {
                MessageBoxResult confirmResult =
                    MessageBox.Show("Are you sure you want to delete the eSight?",
                                        "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (confirmResult == MessageBoxResult.Yes)
                {
                    ESightAppliance appliance = this.ESightApplianceRepo.FilteredItems[Grid.SelectedIndex];
                    this.ESightApplianceRepo.Delete(appliance, DeleteEsightCallback);
                    // OnSearchESight();
                }

            }
        }

        void DeleteEsightCallback(Result result)
        {
            this.ActionResult = result;
            if (!result.Success)
            {
                // TODO 
            }
        }

        private void ShowEditESightDialog(object sender, RoutedEventArgs e)
        {
            if (Grid.SelectedIndex > -1 && Grid.SelectedIndex < this.ESightApplianceRepo.FilteredItems.Count)
            {
                this.Effect = new BlurEffect();
                EditESightDialog dialog = new EditESightDialog(this.ESightApplianceRepo);

                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                dialog.SetItem(this.ESightApplianceRepo.FilteredItems[Grid.SelectedIndex]);
                dialog.ShowDialog();
                this.Effect = null;
            }
        }

        private void ShowAddESightDialog(object sender, RoutedEventArgs e)
        {
            this.Effect = new BlurEffect();
            AddESightDialog dialog = new AddESightDialog(this.ESightApplianceRepo);
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.ShowDialog();
            this.Effect = null;
        }

        public void OnEditESight(string host, string alias, string port, string systemId, string account, string password)
        {
            if (Grid.SelectedIndex > -1 && Grid.SelectedIndex < this.ESightApplianceRepo.FilteredItems.Count)
            {
                // Items[grid.SelectedIndex] = new ServerData(host, alias, port, systemId, account, password);
                OnSearchESight();
            }
        }

        public void OnAddESight(string host, string alias, string port, string systemId, string account, string password)
        {
            // Items.Add(new ServerData(host, alias, port, systemId, account, password));
            OnSearchESight();
        }


        private void OnSearchKeywordDataContextChanged(object Sender, DependencyPropertyChangedEventArgs e)
        {
            OnSearchESight();
        }

        private void OnSearchKeywordChanged(object sender, TextChangedEventArgs e)
        {
            OnSearchESight();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
