﻿/*
 *  Source: MERCURY API >>   ..\mercuryapi-1.31.2.40\cs\Samples\Codelets\Codelets.sln
 *  3.4.2019
 */
using System;
using System.Timers;
//using System.Collections.Generic;
using System.Text;
using System.Linq;
//for Thread.Sleep
using System.Threading;

// Reference the Mercury API
using ThingMagic;

// Because of JSON server ..
using System.Net;

// JSON framework for .NET https://www.newtonsoft.com/json
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.Specialized;


namespace RfidReader
{
    class Program
    {
        static List<IPAddress> servingIP;
        static int rfidTimeout = 400;
        static int rfidAntennapower = 2300;

        static HttpListener _httpListener = new HttpListener();
        static JsonStack js = new JsonStack();

        static Thread rfth;

        static bool ok = true;
        
        static bool serverStillResponding = false;

        static List<string> c;

        
        public class JsonStack
        {
            public string Name { get; set; }
            public DateTime Expiry { get; set; }
            public IList<string> Info { get; set; }
            public bool useCached { get; set; }

            public IList<SimpleObject> UsedParameters { get; set; }

            public string requestType { get; set; }

            public IList<int> availableRFIDList { get; set; }

            public List<string> currentRFIDs { get; set; }

            public List<string> oldListRFIDs { get; set; }

            public List<string> allFoundRFIDs { get; set; }

            public List<string> differenceBetweenCurrentAndFoundRFIDs { get; set; }
        }

        public class SimpleObject
        {
            public string Key { get; set; }
            public string Value { get; set; }
        }

        private static bool keyPressed;

        /*  PARAMETERS what can be changed 
         *  ===>
         *  thresholdValueForCount .. if wanted to block out some RFID data ..
         *  thresholdValueForThreadSleep .. give a value milliseconds
         */
        private static int thresholdValueForCount = 30;
        public static int thresholdValueForThreadSleep = 3000;

        // NOTICE: You might have to change your RFID Reader (Mercury 6) IP address (often)
        // NOTICE: How to do that?
        // NOTICE: --> Open Universal Assistant Reader program in your PC ..
        //string ipAddrFront = "tmr://";
        //string ipAddrBody = "10.103.1.68";
        //string ipAddrEnd = "/";

        static void RFIDInit()
        {
            // NOTICE: Open Universal Assistant Reader program in your PC to get the Merury 6 reader IP address.
            string ipOsoite = "tmr://10.103.1.68/";



            // Create Reader object, connecting to physical device.
            using (Reader r = Reader.Create(ipOsoite))
            {
                // 1st: 
                // Make a connection within given ipAddress
                r.Connect();

                Console.WriteLine("*** CONNECTION SUCCESSFULL .. great .. let's begin ..");

                int currentPower = 0;
                // 2nd:
                // NOTICE: After successful connection set antenna readPower to the right level (max: 27dBm)
                int getAntennaReadPowerValue = (int)r.ParamGet("/reader/radio/readPower");
                Console.WriteLine("*** Antenna Read Power value: " + getAntennaReadPowerValue);
                if (getAntennaReadPowerValue > 2700)
                {
                    int setNewAntennaReadPowerValue = 2300; //TODO:default value & this can be changed later
                    r.ParamSet("/reader/radio/readPower", setNewAntennaReadPowerValue);
                    int getAgainAntennaReadPowerValue = (int)r.ParamGet("/reader/radio/readPower");
                    Console.WriteLine("*** Antenna Read Power (max 27 dBm) exceeded, new value given: " + getAgainAntennaReadPowerValue);
                    currentPower = getAgainAntennaReadPowerValue;
                }
                else
                {
                    Console.WriteLine("else");
                    int setNewAntennaReadPowerValue = rfidAntennapower; //TODO:default value & this can be changed later
                    r.ParamSet("/reader/radio/readPower", setNewAntennaReadPowerValue);
                    int getAgainAntennaReadPowerValue = (int)r.ParamGet("/reader/radio/readPower");
                    Console.WriteLine("*** Antenna Read Power (max 27 dBm) exceeded, new value given: " + getAgainAntennaReadPowerValue);
                    currentPower = getAgainAntennaReadPowerValue;
                }

                /*koitetaan pistää nämä json muotoon*/
                //JsonStack js = new JsonStack();
                js.Name = "Samk M6 MercuryAPI server" + currentPower.ToString();
                js.currentRFIDs = new List<string>();
                js.allFoundRFIDs = new List<string>();
                //TODO: New addition for Console logging purposes. Wanted show EPC only at once.
                List<string> epcList = new List<string>();

                /* IMPORTANT part of the code!
                 * The next delegate function is went through immediatelly when ..
                 * RFID tag is came visible range of antenna.
                 * See more about Thread functionality from docs.
                 */

                // Search for tags in the background
                r.TagRead += delegate (Object sender, TagReadDataEventArgs e)
                {
                    bool epcExistOnList = epcList.Contains(e.TagReadData.EpcString.ToString());
                    if (!epcExistOnList)
                    {
                        Console.WriteLine("----- " + e.TagReadData.Time + " -----");
                        Console.WriteLine("Count: " + e.TagReadData.ReadCount + "]");
                        Console.WriteLine("EPC: " + e.TagReadData.EpcString);
                        //Console.WriteLine("RAW DATA: " + e.TagReadData);
                        epcList.Add(e.TagReadData.EpcString.ToString());
                        js.Expiry = e.TagReadData.Time;
                    }

                    string tagId = e.TagReadData.EpcString.ToString();
                    bool alreadyExist = js.currentRFIDs.Contains(tagId);

                    if (!alreadyExist)
                    {
                        js.currentRFIDs.Add(tagId);
                    }
                };

                

                while (ok)
                {
                    Console.WriteLine("\r\n###");
                    Console.WriteLine("### Reading!, press Esc-key to quit!");
                    Console.WriteLine("###\r\n");
                    r.StartReading();

                    epcList.Clear();
                    Thread.Sleep(80);
                    
                    r.StopReading();
                }

            }


        }



        static void Main(string[] args)
        {
            //RFIDInit();
            //new Thread(new ThreadStart(RFIDInit)).Start();
            rfth = new Thread(new ThreadStart(RFIDInit));
            rfth.Start();

            //ServerInit();
            new Thread(new ThreadStart(ServerInit)).Start();

            //read CMD commands
            new Thread(new ThreadStart(ListenToCommands)).Start();
        }

        static void ListenToCommands() {
            ConsoleKeyInfo cki;

            while (true)
            {
                cki = Console.ReadKey();
                /*
                if (cki.Key == ConsoleKey.D)
                {
                    r.Destroy();
                    System.Environment.Exit(1);
                }
                else if (cki.Key == ConsoleKey.R)
                {
                    r.Reboot();
                    System.Environment.Exit(1);
                }
                else if (cki.Key == ConsoleKey.A)
                {
                    Console.WriteLine("Antenna power" + currentPower);
                    //r.Destroy();
                    //r.Reboot();
                    //System.Environment.Exit(1);
                }
                */
                if (cki.Key == ConsoleKey.Q)
                {
                    Console.WriteLine("|||||||||||||||||||");
                    Console.WriteLine("||||||||||||||||||||");
                    Console.WriteLine("Q pressed: RFIDInit thread aborted. Press W to start it again");
                    Console.WriteLine("||||||||||||||||||||");
                    Console.WriteLine("||||||||||||||||||||");
                    ok = false;

                    //pikku venaus varmuudenvuoksi
                    Thread.Sleep(500);

                    rfth.Join();
                    rfth.Abort();
                }
                else if (cki.Key == ConsoleKey.W)
                {
                    Console.WriteLine("W pressed: RFIDInit thread started. Press Q to stop");
                    ok = true;
                    rfth = new Thread(new ThreadStart(RFIDInit));
                    rfth.Start();
                }
                else if (cki.Key == ConsoleKey.Escape)
                {
                    Console.WriteLine("||||||||Stopping|||||||||");
                    ok = false;
                    Thread.Sleep(500);
                    rfth.Join();
                    rfth.Abort();
                    System.Environment.Exit(1);
                }
            }
        }

        private static void r_ReadException(object sender, ReaderExceptionEventArgs e)
        {
            Console.WriteLine("Error: " + e.ReaderException.Message);
        }

        static void JsonPack()
        {
            //while (true)

            bool readError = false;
            try
            {
                HttpListenerContext context = _httpListener.GetContext();
                HttpListenerRequest request = context.Request;

                //serverStillResponding = true;
                NameValueCollection queryStringCollection = request.QueryString;

                js.Info = new List<string>();
                js.Info.Add("INFO: here are parameters what can be used ..");
                js.Info.Add("How? Add them into IP address in above e.g.   ?timeout=100&antennapower=2300");
                js.Info.Add("PARAMETERS:");
                js.Info.Add("timeout        - with this you can change timeout value (ms) for RFID read loop e.g. 500");
                js.Info.Add("antennapower   - with 4 number value you can change Antenna Read Power value (max: 27 dBm) e.g. 2700");

                js.Info.Add("Server ip(s):");
                foreach (IPAddress ip in servingIP)
                {
                    js.Info.Add("http://" + ip.ToString() + ":5000/");
                }


                if (request.HttpMethod != null)
                {
                    js.requestType = request.HttpMethod;
                }

                //js.UsedParameters = request.QueryString;
                js.UsedParameters = new List<SimpleObject>();
                foreach (String key in queryStringCollection.AllKeys)
                {
                    Console.WriteLine("Key: " + key + " Value: " + queryStringCollection[key]);
                    SimpleObject t = new SimpleObject();
                    t.Key = key;
                    t.Value = queryStringCollection[key];

                    if (t.Key == "timeout")
                    {
                        int tmpValue = System.Convert.ToInt32(t.Value);
                        Console.WriteLine("New timeout value given: " + tmpValue);
                        //TODO: ADD HERE A PUBLIC PARAMETER WHERE YOU CAN SET THIS GIVEN VALUE !?

                        thresholdValueForThreadSleep = tmpValue;
                    }

                    if (t.Key == "antennapower")
                    {
                        int tmpValue = System.Convert.ToInt32(t.Value);
                        if (tmpValue > 2700)
                        {
                            Console.WriteLine("***PROBLEM: given antenna power value: " + tmpValue + " was too high (max: 2700 i.e. 27 dBm), so default value to be used.");
                            string tmpStr = "2700";
                            t.Value = tmpStr;
                            rfidAntennapower = tmpValue;
                        }
                    }

                    //tämä arvo ei muuta mitään rfid threadissa
                    rfth.Abort();


                    Console.WriteLine("New antenna power value given: " + t.Value);

                    rfth = new Thread(new ThreadStart(RFIDInit));
                    rfth.Start();

                    js.UsedParameters.Add(t); // NOTICE that given parameter handling is before that code line.

                }

                Console.WriteLine("count " + js.currentRFIDs.Count);
                Console.WriteLine("count on 0 " + js.currentRFIDs.Count.Equals(0));

                Console.WriteLine("old list ");
                List<string> oldlist = new List<string>();
                oldlist = js.allFoundRFIDs;
                foreach (string i in js.currentRFIDs)
                {
                    Console.WriteLine("- " + i);

                    bool alreadyExist = js.allFoundRFIDs.Contains(i);

                    if (!alreadyExist)
                    {
                        js.allFoundRFIDs.Add(i);
                    }
                }

                Console.WriteLine("compared caches ");
                List<string> comparedList = new List<string>();
                comparedList = oldlist.Except(js.allFoundRFIDs).ToList();
                foreach (string i in comparedList)
                {
                    Console.WriteLine("- " + i);
                }

                Console.WriteLine("compared current ");
                List<string> comparedList2 = new List<string>();
                comparedList2 = oldlist.Except(js.currentRFIDs).ToList();
                foreach (string i in comparedList2)
                {
                    Console.WriteLine("- " + i);
                }

                js.differenceBetweenCurrentAndFoundRFIDs = comparedList2;

                //a.AddRange(b)

                js.oldListRFIDs = new List<string>();

                js.oldListRFIDs = c;
                if (js.currentRFIDs.Count > 0)
                {
                    js.useCached = false;
                    js.Info.Add("Data ok");
                    readError = false;
                }
                else
                {
                    js.useCached = true;
                    js.Info.Add("Data was weird!!!");
                    readError = true;
                }


                string json = JsonConvert.SerializeObject(js);

                if (request.HttpMethod == "GET")
                {
                    // Here i can read all parameters in string but how to parse each one i don't know
                    Console.WriteLine("-NOTE- Server ok");
                }


                byte[] _responseArray = Encoding.UTF8.GetBytes(json);
                context.Response.OutputStream.Write(_responseArray, 0, _responseArray.Length); // write bytes to the output stream

                //context.Response.KeepAlive = false; // set the KeepAlive bool to false
                context.Response.Close(); // close the connection
                Console.WriteLine("Respone given to a request.");
                //json osuus loppuu
                
            }
            catch (Exception ex)
            {
                Console.WriteLine("!!!!!!!!!!!!!!WARNING!!!!!!!!!!!!!!");
                Console.WriteLine("Error: " + ex.Message);
                Console.WriteLine("!!!!!!!!!!!!!!WARNING!!!!!!!!!!!!!!");
                JsonPack();
            }
            finally
            {
                //c = js.currentRFIDs;
                if (!readError)
                { 
                    c = new List<string>();
                    foreach (string i in js.currentRFIDs)
                    {
                        Console.WriteLine("-33 " + i);
                        c.Add(i);
                    }
                }

                Console.WriteLine("Clear ####################################################################");

                js.currentRFIDs.Clear();
               

                foreach (string i in c)
                {
                    Console.WriteLine("-44 " + i);
                }

                //little delay before next response
                //Thread.Sleep(400);
              
                JsonPack();
            }

        }

        static void ServerInit()
        {

            /*
             Windows needs firewall hole 
             Inbound rules and outbound rules need TCP 5000 port open from anywhere
            */
            Console.WriteLine("-------Starting server-------");
            _httpListener.Prefixes.Add("http://localhost:5000/");
            _httpListener.Prefixes.Add("http://127.0.0.1:5000/");
            //_httpListener.Prefixes.Add("http://10.103.1.200:5000/"); // tämä on koneen oma ip joka haettiin cmd ja ipconfig

            Console.WriteLine("Adding IP adresses to list:");

            //https://stackoverflow.com/questions/5271724/get-all-ip-addresses-on-machine
            // Get host name
            String strHostName = Dns.GetHostName();
            // Find host by name
            IPHostEntry iphostentry = Dns.GetHostByName(strHostName);
            // Enumerate IP addresses
            servingIP = new List<IPAddress>();
            foreach (IPAddress ip in iphostentry.AddressList)
            {
                Console.WriteLine("http://" + ip.ToString() + ":5000/");
                _httpListener.Prefixes.Add("http://" + ip.ToString() + ":5000/");

                servingIP.Add(ip);
            }

            _httpListener.Start(); // start server (Run application as Administrator!)
            Console.WriteLine("-------Server started-------");

            //esimerkki json
            Thread _responseThread = new Thread(JsonPack);

            //esimerkki 1
            //Thread _responseThread = new Thread(ResponseThread);

            _responseThread.Start(); // start the response thread
        }
    }
}
