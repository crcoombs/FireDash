using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
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
using System.Xml;

namespace WpfApp1

{
    public class DropLogEntry
    {
        public DateTime EventTime {get; set;}
        public string Application {get; set;}
        public string DestAddress {get; set;}
        public string DestPort {get; set;}
        public string Protocol {get; set;}

        public DropLogEntry(DateTime EventTime, String Application, String DestAddress, String DestPort, String Protocol)
        {
            this.EventTime = EventTime;
            this.Application = Application;
            this.DestAddress = DestAddress;
            this.DestPort = DestPort;
            this.Protocol = Protocol;
        }

        public DropLogEntry()
        {
        }
        override public string ToString()
        {
            return String.Format("{0},{1},{2},{3},{4}", this.EventTime, this.Application, this.DestAddress, this.DestPort, this.Protocol);
        }
    }

    public class DropLog
    {
        public static List<DropLogEntry> GetList()
        {
            string query = "*[System/EventID=5152]";
            EventLogQuery eventsQuery = new EventLogQuery("Security", PathType.LogName, query);
            eventsQuery.ReverseDirection = true;
            List<DropLogEntry> logList = new List<DropLogEntry>();
            EventLogReader logReader = new EventLogReader(eventsQuery);
            for (int i = 0; i< 10; i++)
            {
                EventRecord eventdetail = logReader.ReadEvent();
                using (StringReader eventStringReader = new StringReader(eventdetail.ToXml()))
                {
                    using (XmlReader eventxmlReader = XmlReader.Create(eventStringReader))
                    {
                        DropLogEntry newEntry = new DropLogEntry();
                        while (eventxmlReader.Read())
                        {
                            String systemTime = eventxmlReader.GetAttribute("SystemTime");

                            if (systemTime != null)
                            {
                                newEntry.EventTime = Convert.ToDateTime(systemTime);
                            }
                            switch (eventxmlReader.GetAttribute("Name"))
                            {
                                case "Application":
                                    {
                                        String applicationString = eventxmlReader.ReadElementContentAsString();
                                        if(applicationString.Contains('\\'))
                                        {
                                            applicationString = applicationString.Split('\\').Last();
                                        }
                                        newEntry.Application = applicationString;
                                        break;
                                    }
                                case "DestAddress":
                                    {
                                        newEntry.DestAddress = eventxmlReader.ReadElementContentAsString();
                                        break;
                                    }
                                case "DestPort":
                                    {
                                        newEntry.DestPort = eventxmlReader.ReadElementContentAsString();
                                        break;
                                    }
                                case "Protocol":
                                    {
                                        String protocolString = "UDP";
                                        int protocolNum = eventxmlReader.ReadElementContentAsInt();
                                        if(protocolNum == 6)
                                        {
                                            protocolString = "TCP";
                                        }
                                        newEntry.Protocol = protocolString;
                                        break;
                                    }
                            }
                        }
                        logList.Add(newEntry);
                    }
                }
            }
            return logList;
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
            List<DropLogEntry> logList = DropLog.GetList();
            DropListGrid.ItemsSource = logList;
        }
    }
}
 