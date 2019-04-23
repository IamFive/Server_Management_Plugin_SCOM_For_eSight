﻿using System;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel.Composition;

namespace Huawei.SCOM.ESightPlugin.ViewLib
{

    //[Export]
    //[PartCreationPolicy(CreationPolicy.NonShared)]

    public partial class ESightHealthDashboard : UserControl, INotifyPropertyChanged
    {
        public ESightHealthDashboard()
        {
            InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void AddBladeServerSeries(HealthStatus status, int count)
        {
            BladePieChart.AddSeries(status, count);
        }

        public void AddHighDensityServerSeries(HealthStatus status, int count)
        {
            HighDensityPieChart.AddSeries(status, count);
        }

        public void AddRackServerSeries(HealthStatus status, int count)
        {
            RackPieChart.AddSeries(status, count);
        }

        public void AddKunLunServerSeries(HealthStatus status, int count)
        {
            KunLunPieChart.AddSeries(status, count);
        }
    }
}
