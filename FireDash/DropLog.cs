using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Xml.Linq;

namespace FireDash
{
    //Defines an entry in the log.
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

        public DropLogEntry() { }

        override public string ToString()
        {
            return String.Format("{0},{1},{2},{3},{4},{5},{6},{7}", this.EventTime, this.Application, this.SourceAddress, this.SourcePort, this.DestAddress, this.DestPort, this.Protocol, this.Direction);
        }
    }

    //An entry in the Top 10 list.
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

    //A collection of DropLogEntries.
    public class DropLog
    {
        // Converts Event Log entries into a Collection for display
        public static ObservableCollection<DropLogEntry> GetList()
        {
            string startTime = DateTime.UtcNow.AddMinutes(-10).ToString("o"); //10 minutes
            string eventID = "5152";
            string query = String.Format("*[System/EventID={0}] and *[System[TimeCreated[@SystemTime >= '{1}']]]", eventID, startTime);

            EventLogQuery eventsQuery = new EventLogQuery("Security", PathType.LogName, query)
            {
                ReverseDirection = true
            };
            ObservableCollection<DropLogEntry> logList = new ObservableCollection<DropLogEntry>();
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
            List<Top10Entry> hitList = new List<Top10Entry>();
            Dictionary<string, int> addressHits = new Dictionary<string, int>();

            foreach (DropLogEntry entry in logList)
            {
                String propertyValue = (String)entry.GetType().GetProperty(property).GetValue(entry);
                if (addressHits.ContainsKey(propertyValue))
                {
                    addressHits[propertyValue] += 1;
                }
                else
                {
                    addressHits[propertyValue] = 1;
                }
            }

            foreach (KeyValuePair<string, int> record in addressHits)
            {
                hitList.Add(new Top10Entry(record.Key, record.Value));
            }
            hitList.Sort();

            if (hitList.Count > 10)
            {
                hitList = hitList.GetRange(0, 10);
            }
            return new ObservableCollection<Top10Entry>(hitList);
        }
    }
}
