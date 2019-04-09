/*
 *  Source: MERCURY API >>   ..\mercuryapi-1.31.2.40\cs\Samples\Codelets\Codelets.sln
 *  3.4.2019 by Hypehanke
 *  Mercury Api tarvitsee ladata täältä https://www.jadaktech.com/documentation/rfid/mercuryapi/
 *  Lisätään MercuryAPI.dll käyttöön painamalla References päälä oikeaa nappia ja valitaan Add Reference
 *  Valitaan vasemalta Browse alhaalta nappi Browse haetaan puretusta kansiosta(esim. C:\mercuryapi-1.31.2.40\cs) MercuryAPI.dll tiedosto
 */
using System;
//using System.Collections.Generic;
using System.Text;
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
        /* PARAMETER(s): */
        static int rfidTimeout = 400;               //NOTE: RFID timeout value (ms), used inside of While-loop
        static int rfidAntennaPowerValue = 2300;    //NOTE: Max value is (27 dBm) -> 2700
        static int rfidCountThreshold = 30;         //NOTE: E.g. limit for RFID count value, if wanted to block some tags out?
        // RFID Reader IP Address: if/when it has changed -> open Universal Reader Assistant program ..
        static string ipAddress = "tmr://10.103.1.68/";

        static HttpListener _httpListener = new HttpListener();
        static JsonStack js = new JsonStack();

        static Thread publicRfidThread;

        public class JsonStack
        {
            public string Name { get; set; }
            public DateTime Expiry { get; set; }
            public IList<string> Info { get; set; }
            public IList<SimpleObject> UsedParameters { get; set; }
            public string requestType { get; set; }
            public IList<int> availableRFIDList { get; set; }
            public IList<string> rfidDataToString { get; set; }
        }

        public class SimpleObject
        {
            public string Key { get; set; }
            public string Value { get; set; }
        }

        static void RFIDInit() {

            try
            {
                
                // Create RFID Reader object, connecting to physical device.
                using (Reader r = Reader.Create(ipAddress))
                {
                    // 1st: 
                    // Make a connection within given ipAddress
                    r.Connect();
                    Console.WriteLine("*** CONNECTION SUCCESSFULL .. great .. let's begin ..");

                    // 2nd:
                    // NOTE: IMPORTANT !!
                    // After successful connection verify RFID antenna readPower value..
                    // maximum value is (max: 27dBm), use integer value within 4 numbers --> e.g. 2700
                    int getAntennaReadPowerValue = (int)r.ParamGet("/reader/radio/readPower");
                    Console.WriteLine("*** Antenna readPower value: " + getAntennaReadPowerValue);
                    if (getAntennaReadPowerValue > 2700)
                    {
                        //Note: progress in this If-statement..
                        //1st new public antenna power is given and 2nd printed out is the given value changed or not
                        //r.ParamSet("/reader/radio/readPower", rfidAntennaPowerValue);
                        r.ParamSet("/reader/radio/writePower", rfidAntennaPowerValue);
                        int testReadPowerValue = (int)r.ParamGet("/reader/radio/readPower");
                        Console.WriteLine("*** Antenna readPower (max 27 dBm) exceeded, new value given: " + testReadPowerValue);
                    }
                    else {
                        //Note: progress in this Else-statement..
                        //if RFID Reader antenna power value differs from public value -> public value is used
                        if(getAntennaReadPowerValue != rfidAntennaPowerValue)
                        {
                            r.ParamSet("/reader/radio/readPower", rfidAntennaPowerValue);
                            Console.WriteLine("*** Rfid public antenna readPower value given: " + rfidAntennaPowerValue);
                        }
                        else
                        {
                            Console.WriteLine("*** Antenna readPower stayed as it was inside RFID reader: " + getAntennaReadPowerValue + ", Rfid public antenna readPower value: " + rfidAntennaPowerValue);
                            rfidAntennaPowerValue = getAntennaReadPowerValue;
                        }
                    }

                    /*koitetaan pistää nämä json muotoon*/
                    //JsonStack js = new JsonStack();
                    //TODO:js.Name = "Samk M6 MercuryAPI server, antenna power: " + currentPower.ToString();
                    js.Name = "Samk M6 MercuryAPI server, antenna power: " + rfidAntennaPowerValue.ToString();
                    js.rfidDataToString = new List<string>();

                    //Note:
                    //Next string list is made for Console logging purposes. Wanted to shows EPC data only at once.
                    List<string> epcList = new List<string>();

                    /* IMPORTANT part of the code!
                     * The next delegate function is went through immediatelly when ..
                     * RFID tag is came visible range of antenna.
                     * See more about Thread functionality from docs and ..
                     * Mercury API: ..\mercuryapi-1.31.2.40\cs\Samples\Codelets\ReadAsync\ReadAsync.cs
                     */

                    // Create and add tag listener
                    r.TagRead += delegate (Object sender, TagReadDataEventArgs e)
                    {
                        //Note: The next IF-statement for Console logging purposes only ..
                        bool epcExistOnList = epcList.Contains(e.TagReadData.EpcString.ToString());
                        if(!epcExistOnList)
                        {
                            Console.WriteLine("RFID tag data: [count: " + e.TagReadData.ReadCount + "], EPC: " + e.TagReadData.EpcString + ", RAW DATA: " + e.TagReadData);
                            epcList.Add(e.TagReadData.EpcString.ToString());
                        }
                        /*TODO: next readCount threshold functionality commented out. Fix it if necessarily to take account..
                        if (e.TagReadData.ReadCount < rfidCountThreshold)
                        {
                            Console.WriteLine("***NOT a GOOD - RFID DATA: [count: " + e.TagReadData.ReadCount + "]: " + e.TagReadData);
                        }
                        else
                        {
                            Console.WriteLine("RFID DATA: [count: " + e.TagReadData.ReadCount + "], EPC: " + e.TagReadData.EpcString);
                        }*/

                        //Note: The next IF-statement for JSON server purposes ..
                        bool alreadyExist = js.rfidDataToString.Contains(e.TagReadData.EpcString.ToString());
                        if (!alreadyExist)
                        {
                            js.rfidDataToString.Add(e.TagReadData.EpcString.ToString());
                        }
                    };

                    // Create and add read exception listener
                    r.ReadException += new EventHandler<ReaderExceptionEventArgs>(r_ReadException);

                    while (true)
                    {
                        // Search for tags in the background
                        r.StartReading();
                        Console.WriteLine("r.StartReading() .. QUIT reading with CTRL + C");
                        js.rfidDataToString.Clear();
                        epcList.Clear();
                        Thread.Sleep(rfidTimeout);
                    }

                    r.StopReading();
                    Console.WriteLine("r.StopReading().");
                }
            }
            catch (ReaderException re)
            {
                Console.WriteLine("Error: " + re.Message);
                Console.Out.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }

            //TODO: SEURAAVALLA READLINE:LLA PYSÄYTETÄÄN OHJELMA PYSÄHTYMÄSTÄ ILMAN KUITTAUSTA
            Console.ReadLine();
        }

        static void Main(string[] args)
        {
            //RFIDInit();
            publicRfidThread = new Thread(new ThreadStart(RFIDInit));
            publicRfidThread.Start();

            //ServerInit();
            new Thread(new ThreadStart(ServerInit)).Start();
        }

        private static void r_ReadException(object sender, ReaderExceptionEventArgs e)
        {
            Console.WriteLine("Error: " + e.ReaderException.Message);
        }

        static void JsonPack() {
            while (true)
            {
                HttpListenerContext context = _httpListener.GetContext();
                HttpListenerRequest request = context.Request;

                NameValueCollection queryStringCollection = request.QueryString;
                
                js.Info = new List<string>();
                js.Info.Add("INFO: here are parameters what can be used ..");
                js.Info.Add("How? Add them into IP address in above e.g.   ?timeout=100&antennapower=2300");
                js.Info.Add("PARAMETERS:");
                js.Info.Add("timeout        - with this you can change timeout value (ms) for RFID read loop e.g. 500");
                js.Info.Add("antennapower   - with 4 number value you can change Antenna Read Power value (max: 27 dBm) e.g. 2700");

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
                        if (tmpValue < 10)
                        {
                            Console.WriteLine("New timeout value is less than 10 ms -- not approved: " + tmpValue + ", default value 400 ms given");
                            rfidTimeout = 400;
                        }
                        if (tmpValue > 10000)
                        {
                            Console.WriteLine("New timeout value is more than 10000 ms: " + tmpValue);
                            rfidTimeout = tmpValue;
                        }
                        else
                        {
                            Console.WriteLine("New timeout value is given: " + tmpValue);
                            rfidTimeout = tmpValue;
                        }
                        //TODO: ADD HERE A PUBLIC PARAMETER WHERE YOU CAN SET THIS GIVEN VALUE !?
                    }

                    if (t.Key == "antennapower")
                    {
                        int tmpValue = System.Convert.ToInt32(t.Value);
                        if (tmpValue > 2700)
                        {
                            Console.WriteLine("***PROBLEM: given antenna power value: " + tmpValue + " was too high (max: 2700 i.e. 27 dBm), so default value to be used.");
                            string tmpStr = "2700";
                            t.Value = tmpStr;
                        }
                    }
                    js.UsedParameters.Add(t); // NOTICE that given parameter handling is before that code line.

                    //tämä arvo ei muuta mitään rfid threadissa
                    publicRfidThread.Abort();

                    rfidAntennaPowerValue = tmpValue;
                    Console.WriteLine("New antenna power value given: " + t.Value);

                    publicRfidThread = new Thread(new ThreadStart(RFIDInit));
                    publicRfidThread.Start();
                }

                string json = JsonConvert.SerializeObject(js);

                if (request.HttpMethod == "GET")
                {
                    // Here i can read all parameters in string but how to parse each one i don't know
                    Console.WriteLine("Got get parameter(s)");
                }

                byte[] _responseArray = Encoding.UTF8.GetBytes(json);
                context.Response.OutputStream.Write(_responseArray, 0, _responseArray.Length); // write bytes to the output stream

                //context.Response.KeepAlive = false; // set the KeepAlive bool to false
                context.Response.Close(); // close the connection
                Console.WriteLine("Response given to a request.");
                //json osuus loppuu
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
            foreach (IPAddress ip in iphostentry.AddressList)
            {
                Console.WriteLine("http://" + ip.ToString() + ":5000/");
                _httpListener.Prefixes.Add("http://" + ip.ToString() + ":5000/");
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
