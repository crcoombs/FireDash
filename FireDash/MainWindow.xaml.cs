﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Xml.Linq;

namespace FireDash

{
    public class DropLogEntry
    {
        private Dictionary<String, String> _protocols = new Dictionary<String, String>
        {
            {"1", "ICMP" },
            {"6", "TCP" },
            {"17", "UDP" }
        };
        private string _protocol;
        private Dictionary<String, String> _directions = new Dictionary<String, String>
        {
            {"%%14592", "Inbound" },
            {"%%14593", "Outbound" }
        };
        private string _direction;

        [DisplayName("Time")]
        public DateTime EventTime { get; set; }
        public string Application { get; set; }
        [DisplayName("Source Address")]
        public string SourceAddress { get; set; }
        [DisplayName("Source Port")]
        public string SourcePort { get; set; }
        [DisplayName("Destination Address")]
        public string DestAddress { get; set; }
        [DisplayName("Destination Port")]
        public string DestPort { get; set; }
        public string Protocol { get => _protocols[_protocol]; set => _protocol = value; }
        public string Direction { get => _directions[_direction]; set => _direction = value; }

        public DropLogEntry(DateTime EventTime, String Application, String SourceAddress, String SourcePort, String DestAddress, String DestPort, String Protocol, String Direction)
        {
            this.EventTime = EventTime;
            this.Application = Application;
            this.SourceAddress = SourceAddress;
            this.SourcePort = SourcePort;
            this.DestAddress = DestAddress;
            this.DestPort = DestPort;
            this.Protocol = Protocol;
            this.Direction = Direction;
        }

        public DropLogEntry(){}

        override public string ToString()
        {
            return String.Format("{0},{1},{2},{3},{4},{5},{6},{7}", this.EventTime, this.Application, this.SourceAddress, this.SourcePort, this.DestAddress, this.DestPort, this.Protocol, this.Direction);
        }
    }

    //Dictionaries don't implement IList, so we'll wrap it in a class
    public class Top10Entry : IComparable<Top10Entry>
    {
        public string Address { get; set; }
        public int Hits { get; set; }

        public Top10Entry() { }
        public Top10Entry(string address, int hits)
        {
            this.Address = address;
            this.Hits = hits;
        }

        public int CompareTo(Top10Entry other)
        {
            return other.Hits - this.Hits;
        }
    }

    public class DropLog
    {
        // Converts Event Log entries into a Collection for display
        public static ObservableCollection<DropLogEntry> GetList()
        {
            var tenMinutes = DateTime.UtcNow.AddMinutes(-10).ToString("o");
            var eventID = 5152;
            string query = String.Format("*[System/EventID={0}] and *[System[TimeCreated[@SystemTime >= '{1}']]]", eventID, tenMinutes);
            EventLogQuery eventsQuery = new EventLogQuery("Security", PathType.LogName, query)
            {
                ReverseDirection = true
            };
            ObservableCollection<DropLogEntry> logList = new ObservableCollection<DropLogEntry>();
            EventLogReader logReader = new EventLogReader(eventsQuery);
            Trace.TraceInformation("Query successful.");
            for (EventRecord eventdetail = logReader.ReadEvent(); eventdetail != null; eventdetail = logReader.ReadEvent())
            {
                DropLogEntry entry = new DropLogEntry();
                XElement eventElement = XElement.Parse(eventdetail.ToXml());
                entry.EventTime = DateTime.Parse(eventElement.Descendants("{http://schemas.microsoft.com/win/2004/08/events/event}TimeCreated").Single().Attribute("SystemTime").Value);
                foreach (XElement node in eventElement.Descendants("{http://schemas.microsoft.com/win/2004/08/events/event}Data"))
                {
                    String nodeName = node.Attributes("Name").Single().Value;
                    switch (nodeName)
                    {
                        case "Application":
                        case "Direction":
                        case "SourceAddress":
                        case "SourcePort":
                        case "DestAddress":
                        case "DestPort":
                        case "Protocol": 
                            entry.GetType().GetProperty(nodeName).SetValue(entry, node.Value);
                            break;
                    }
                }
                logList.Add(entry);
            }
            return logList;
        }

        // Gets all outbound events
        public static ObservableCollection<DropLogEntry> GetOutboundList(ObservableCollection<DropLogEntry> logList)
        {
            return new ObservableCollection<DropLogEntry>(logList.Where(entry => entry.Direction.Equals("Outbound")));
        }

        // Gets all inbound evernts
        public static ObservableCollection<DropLogEntry> GetInboundList(ObservableCollection<DropLogEntry> logList)
        {
            return new ObservableCollection<DropLogEntry>(logList.Where(entry => entry.Direction.Equals("Inbound")));
        }

        // Generates a Top 10 list for a specified property
        public static ObservableCollection<Top10Entry> GetTop10(ObservableCollection<DropLogEntry> logList, String property)
        {
            // List needed here because there's no sort method for ObservableCollections
            var hitList = new List<Top10Entry>();
            Dictionary<string, int> addressHits = new Dictionary<string, int>();
            foreach (var entry in logList)
            {
                String propertyValue = (String) entry.GetType().GetProperty(property).GetValue(entry);
                if (addressHits.ContainsKey(propertyValue))
                {
                    addressHits[propertyValue] += 1;
                }
                else
                {
                    addressHits[propertyValue] = 1;
                }
            }
            foreach(var record in addressHits)
            {
                hitList.Add(new Top10Entry(record.Key, record.Value));
            }
            hitList.Sort();
            if(hitList.Count > 10)
            {
                hitList = hitList.GetRange(0, 10);
            }
            return new ObservableCollection<Top10Entry>(hitList);
        }
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<DropLogEntry> _droplist;
        private ObservableCollection<DropLogEntry> _inbounddroplist;
        private ObservableCollection<DropLogEntry> _outbounddroplist;
        private ObservableCollection<Top10Entry> _inboundtop10;
        private ObservableCollection<Top10Entry> _outboundtop10;

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
            _droplist = DropLog.GetList();
            _inbounddroplist = DropLog.GetInboundList(_droplist);
            _outbounddroplist = DropLog.GetOutboundList(_droplist);
            _inboundtop10 = DropLog.GetTop10(_inbounddroplist, "SourceAddress");
            _outboundtop10 = DropLog.GetTop10(_outbounddroplist, "DestAddress");
            DropListGrid.ItemsSource = _droplist;
            SearchButton_OnClick(null,null);
            OutboundTop10Grid.ItemsSource = _outboundtop10;
            InboundTop10Grid.ItemsSource = _inboundtop10;
        }

        // Filters the main list based on text box
        // Filter format: property1=value1 property2=value2
        void SearchButton_OnClick(object sender, RoutedEventArgs e)
        {
            var _itemSourceList = new CollectionViewSource() { Source = _droplist };
            String[] searchTerms = SearchBox.Text.Split(' ');          
            foreach (String searchTerm in searchTerms)
            {
                String[] searchArgs = searchTerm.Split('=');
                _itemSourceList.Filter += (sender2, e2) => propertyfilter(sender2, e2, searchArgs);
            }
            ICollectionView Itemlist = _itemSourceList.View;

            DropListGrid.ItemsSource = Itemlist;
        }

        // Filters a CollectionViewSource by searching for a specified combination of property name and value
        private void propertyfilter(object sender, FilterEventArgs e, String[] searchArgs)
        {
            var entry = e.Item as DropLogEntry;
            var propInfo = entry.GetType().GetProperty(searchArgs[0], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (propInfo != null)
            {
                if (propInfo.GetValue(entry).ToString().Equals(searchArgs[1], StringComparison.OrdinalIgnoreCase))
                {
                    e.Accepted = true;
                }
                else
                {
                    e.Accepted = false;
                }
            }
        }

        private void DropListGrid_AutoGeneratingColumn(object sender, System.Windows.Controls.DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.PropertyDescriptor is PropertyDescriptor descriptor)
            {
                e.Column.Header = descriptor.DisplayName ?? descriptor.Name;
            }
        }
    }
}
 