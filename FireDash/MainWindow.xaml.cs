using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Windows;
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

        public DateTime EventTime { get; set; }
        public string Application { get; set; }
        public string SourceAddress { get; set; }
        public string SourcePort { get; set; }
        public string DestAddress { get; set; }
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
        public static List<DropLogEntry> GetList()
        {
            var tenMinutes = DateTime.UtcNow.AddMinutes(-10).ToString("o");
            var eventID = 5152;
            string query = String.Format("*[System/EventID={0}] and *[System[TimeCreated[@SystemTime >= '{1}']]]", eventID, tenMinutes);
            EventLogQuery eventsQuery = new EventLogQuery("Security", PathType.LogName, query);
            eventsQuery.ReverseDirection = true;
            List<DropLogEntry> logList = new List<DropLogEntry>();
            EventLogReader logReader = new EventLogReader(eventsQuery);
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

        public static List<DropLogEntry> GetOutboundList()
        {
            var logList = DropLog.GetList();
            return logList.Where(entry => entry.Direction.Equals("Outbound")).ToList();
        }

        public static List<DropLogEntry> GetInboundList()
        {
            var logList = DropLog.GetList();
            return logList.Where(entry => entry.Direction.Equals("Inbound")).ToList();
        }

        public static List<Top10Entry> GetOutboundTop10()
        {
            var hitList = new List<Top10Entry>();
            var outboundList = GetOutboundList();
            Dictionary<string, int> addressHits = new Dictionary<string, int>();
            foreach (var entry in outboundList)
            {
                if (addressHits.ContainsKey(entry.DestAddress))
                {
                    addressHits[entry.DestAddress] += 1;
                }
                else
                {
                    addressHits[entry.DestAddress] = 1;
                }
            }
            foreach(var record in addressHits)
            {
                hitList.Add(new Top10Entry(record.Key, record.Value));
            }
            hitList.Sort();
            return hitList;
        }

        public static List<Top10Entry> GetInboundTop10()
        {
            var hitList = new List<Top10Entry>();
            var inboundList = GetInboundList();
            Dictionary<string, int> addressHits = new Dictionary<string, int>();
            foreach (var entry in inboundList)
            {
                if (addressHits.ContainsKey(entry.SourceAddress))
                {
                    addressHits[entry.SourceAddress] += 1;
                }
                else
                {
                    addressHits[entry.SourceAddress] = 1;
                }
            }
            foreach (var record in addressHits)
            {
                hitList.Add(new Top10Entry(record.Key, record.Value));
            }
            hitList.Sort();
            return hitList;
        }
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.KeyDown += new KeyEventHandler(MainWindow_KeyDown);
            RefreshLists();
        }

        void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                RefreshLists();
            }
        }

        void RefreshLists()
        {
            OutboundDropListGrid.ItemsSource = DropLog.GetOutboundList();
            InboundDropListGrid.ItemsSource = DropLog.GetInboundList();
            OutboundTop10Grid.ItemsSource = DropLog.GetOutboundTop10();
            InboundTop10Grid.ItemsSource = DropLog.GetInboundTop10();
        }
    }
}
 