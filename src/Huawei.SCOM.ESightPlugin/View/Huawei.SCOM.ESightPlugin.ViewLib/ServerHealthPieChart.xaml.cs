using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;

namespace Huawei.SCOM.ESightPlugin.ViewLib
{

    public enum HealthStatus
    {
        OK,
        Warning,
        Critical,
        Unknown,
    };


    public partial class ServerHealthPieChart : UserControl
    {
        private LiveCharts.Wpf.PieChart chart;
        public Func<ChartPoint, string> PointLabel { get; set; }

        public ServerHealthPieChart()
        {
            InitializeComponent();
            PointLabel = series => string.Format("{0} ({1:P})", series.Y, series.Participation);
            DataContext = this;
        }


        public void AddSeries(HealthStatus status, int count)
        {
            SetAvailable(true);
            foreach (PieSeries series in chart.Series)
            {
                if (series.Title.Equals(Enum.GetName(typeof(HealthStatus), status), StringComparison.InvariantCultureIgnoreCase))
                {
                    series.Values = new ChartValues<double> { Convert.ToDouble(count) };
                    switch (status)
                    {
                        case HealthStatus.Critical:
                            lblCritical.Content = Convert.ToString(count);
                            break;
                        case HealthStatus.Warning:
                            lblWarning.Content = Convert.ToString(count);
                            break;
                        case HealthStatus.OK:
                            lblOk.Content = Convert.ToString(count);
                            break;
                        case HealthStatus.Unknown:
                            lblUnknown.Content = Convert.ToString(count);
                            break;
                        default:
                            break;
                    }
                }
            }
        }


        public string Title
        {
            get { return box_title.Header.ToString(); }
            set { box_title.Header = value; }
        }

        private void PieChart_Loaded(object sender, RoutedEventArgs e)
        {
            chart = sender as LiveCharts.Wpf.PieChart;
        }

        private void SetAvailable(bool isAvailable)
        {
            if (!isAvailable)
            {
                lblNoData.Visibility = Visibility.Visible;
                chartZone.Visibility = Visibility.Visible;
                chartLblZone.Visibility = Visibility.Visible;
            }
            else
            {
                lblNoData.Visibility = Visibility.Hidden;
                chartZone.Visibility = Visibility.Hidden;
                chartLblZone.Visibility = Visibility.Hidden;
            }
        }
    }

}
