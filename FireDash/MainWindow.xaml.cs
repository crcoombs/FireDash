using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace FireDash
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<DropLogEntry> _dropList;
        private ObservableCollection<DropLogEntry> _inboundDropList;
        private ObservableCollection<DropLogEntry> _outboundDropList;
        private ObservableCollection<Top10Entry> _inboundTop10;
        private ObservableCollection<Top10Entry> _outboundTop10;

        public MainWindow()
        {
            InitializeComponent();
            this.KeyDown += new KeyEventHandler(MainWindow_KeyUp);
            SearchButton.Click += SearchButton_OnClick;
            RefreshLists();
        }

        // F5 to refresh
        void MainWindow_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                RefreshLists();
            }
        }

        // Gets fresh list
        void RefreshLists()
        {
            _dropList = DropLog.GetList();
            _inboundDropList = DropLog.GetInboundList(_dropList);
            _outboundDropList = DropLog.GetOutboundList(_dropList);
            _inboundTop10 = DropLog.GetTop10(_inboundDropList, "SourceAddress");
            _outboundTop10 = DropLog.GetTop10(_outboundDropList, "DestAddress");

            DropListGrid.ItemsSource = _dropList;
            SearchButton_OnClick(null,null); //Maintain any search filters in place prior to refresh
            OutboundTop10Grid.ItemsSource = _outboundTop10;
            InboundTop10Grid.ItemsSource = _inboundTop10;
        }

        // Filters the main list based on text box
        // Filter format: property1=value1 property2=value2
        void SearchButton_OnClick(object sender, RoutedEventArgs e)
        {
            var _itemSourceList = new CollectionViewSource() { Source = _dropList };
            String[] searchTerms = SearchBox.Text.Split(',');          
            foreach (String searchTerm in searchTerms)
            {
                String[] searchArgs = searchTerm.Split('=');
                _itemSourceList.Filter += (sender2, e2) => PropertyFilter(sender2, e2, searchArgs);
            }
            ICollectionView Itemlist = _itemSourceList.View;

            DropListGrid.ItemsSource = Itemlist;
        }

        // Filters a CollectionViewSource by searching for a specified combination of property name and value
        private void PropertyFilter(object sender, FilterEventArgs e, String[] searchArgs)
        {
            if (searchArgs[0] == "")
            {
                e.Accepted = true;
                return;
            }

            String propertyName = searchArgs[0];
            String propertyValue = searchArgs[1];
            var entry = e.Item as DropLogEntry;

            foreach (var property in entry.GetType().GetProperties())
            {
                DisplayNameAttribute displayname = property.GetCustomAttribute(typeof(DisplayNameAttribute)) as DisplayNameAttribute;
                if (displayname != null)
                { 
                    if (displayname.DisplayName.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        propertyName = property.Name;
                    }
                }
            }

            var propInfo = entry.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (propInfo != null)
            {
                if (propInfo.GetValue(entry).ToString().Equals(propertyValue, StringComparison.OrdinalIgnoreCase))
                {
                    e.Accepted = true;
                }
                else
                {
                    e.Accepted = false;
                }
            }
        }

        //Sets column headers to DisplayNames if defined, fall back to property name if not 
        private void DropListGrid_AutoGeneratingColumn(object sender, System.Windows.Controls.DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.PropertyDescriptor is PropertyDescriptor descriptor)
            {
                e.Column.Header = descriptor.DisplayName ?? descriptor.Name;
            }
        }
    }
}
 