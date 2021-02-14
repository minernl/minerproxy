﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using MinerProxy.Logging;
using MinerProxy.Configs;
namespace MinerProxy
{
    public class Settings
    {

        public int proxyListenPort { get; set; }
        //public string remotePoolAddress { get; set; }
        //public int remotePoolPort { get; set; }
        public int webSocketPort { get; set; }
        public bool log { get; set; }
        public bool debug { get; set; }
        public bool identifyDevFee { get; set; }
        public bool showEndpointInConsole { get; set; }
        public bool showRigStats { get; set; }
        public bool useDotWithRigName { get; set; }
        public bool useSlashWithRigName { get; set; }
        public bool useWorkerWithRigName { get; set; }
        public bool colorizeConsole { get; set; }
        public bool replaceWallet { get; set; }
        public bool useWebSockServer { get; set; }
        public bool usePasswordAsRigName { get; set; }
        public bool donateDevFee { get; set; }
        public bool useRigNameAsEndPoint { get; set; }
        public int percentToDonate { get; set; }
        public int rigStatsIntervalSeconds { get; set; }
        public string walletAddress { get; set; }
        public string devFeeWalletAddress { get; set; }
        public string minedCoin { get; set; }
        public List<string> allowedAddresses = new List<string>();
        internal bool consoleQueueStarted { get; set; }
        internal string settingsFile { get; set; }

        public List<PoolItem> poolList = new List<PoolItem>();
        private int poolIndex = 0;
        private int failedConnects = 1;

        public int IncrementFailedConnects() { return failedConnects++; }
        public void ResetFailedConnects() { failedConnects = 1; }

        public PoolItem GetCurrentPool()
        {
            if (poolList.Count == 0)
                throw new Exception("No pools in the list");

            if (failedConnects >= 4)
            {
                failedConnects = 0;
                return GetNextPool();
            }

            return poolList[poolIndex];

        }

        public PoolItem GetNextPool(bool hideLog = false)
        {
            if (poolList.Count == 0)
                throw new Exception("No pools in the list");

            if (poolList.Count == 1)
                return poolList[0];

            var eval = ((poolIndex + 1) >= poolList.Count) ? poolIndex = 0 : poolIndex++;

            while (string.IsNullOrWhiteSpace(poolList[poolIndex].poolAddress) || (poolList[poolIndex].poolPort == 0))
            {
                GetNextPool(true);
            }

            if (!hideLog)
                Logger.LogToConsole(string.Format("Pool switching to [{0}:{1}]", poolList[poolIndex].poolAddress, poolList[poolIndex].poolPort),"FAILOVER", ConsoleColor.Red);
            return poolList[poolIndex];

        }

        public static void LoadSettings(out Settings settings, string settingsJson = "settings.json")
        {
            
            settings = new Settings();
            if (File.Exists(settingsJson))
            {
                Logger.LogToConsole(string.Format("Loading settings from {0}", settingsJson));
                try
                {
                    settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(settingsJson));
                    if (settings.proxyListenPort == 0)
                    {
                        IncorrectSettingsMessage("Local port missing!", settings, settingsJson);
                    }

                    /*
                    if (string.IsNullOrEmpty(settings.remotePoolAddress))
                    {
                        IncorrectSettingsMessage("Remote host missing!", settings, settingsJson);
                    }
                    if (settings.remotePoolPort == 0)
                    {
                        IncorrectSettingsMessage("Remote port missing!", settings, settingsJson);
                    }
                    */

                    if (settings.allowedAddresses.Count == 0)
                    {
                        IncorrectSettingsMessage("No allowed addresses!", settings, settingsJson);
                    }
                    if (settings.useWebSockServer && settings.webSocketPort == 0)
                    {
                        IncorrectSettingsMessage("WebSock enabled, but no port set!", settings, settingsJson);
                    }
                    if (string.IsNullOrEmpty(settings.walletAddress))
                    {
                        IncorrectSettingsMessage("Wallet address missing!", settings, settingsJson);
                    }
                    if ((string.IsNullOrEmpty(settings.minedCoin)) | !ValidateCoin(settings.minedCoin))
                    {
                        IncorrectSettingsMessage(string.Format("Unknown coin specified {0}", settings.minedCoin), settings, settingsJson);
                    }
                    else
                    {
                        settings.minedCoin = settings.minedCoin.ToUpper();
                    }
                    if (string.IsNullOrEmpty(settings.devFeeWalletAddress))
                    {
                        settings.devFeeWalletAddress = "";
                    }
                    if (settings.poolList.Count == 0)
                    {
                        IncorrectSettingsMessage("No pool information was found!", settings, settingsJson);
                    }

                    for (int i = 0; i < settings.poolList.Count; i++)
                    {
                        if (string.IsNullOrWhiteSpace(settings.poolList[i].poolAddress) || (settings.poolList[i].poolPort == 0))
                        {
                            if (i==0)
                                IncorrectSettingsMessage("At least the irst pool Address and Port must be specified!", settings, settingsJson);

                            Console.WriteLine("Pool information " + (i + 1) + " blank; Skipping.", settings, settingsJson);
                        }
                    }
                    settings.minedCoin = settings.minedCoin.ToUpper();
                    settings.settingsFile = settingsJson;
                    return;

                }
                catch (Exception ex)
                {
                    IncorrectSettingsMessage(string.Format("Unable to load {0}", ex.Message), settings, settingsJson);
                    System.Environment.Exit(1);
                }
            }
            else
            {
                Logger.LogToConsole(string.Format("No {0} found! Generating generic one", settingsJson));

                settings = GetGenericSettings(settings);

                writeSettings(settingsJson, settings);

                Logger.LogToConsole(string.Format("Edit the new {0} file and don't forget to change the wallet address!", settingsJson));
                Console.Write("Press any key to exit..");
                Console.ReadKey();
                System.Environment.Exit(1);
            }
        }

        public static void writeSettings(string settingsJson, Settings settings)
        {
            try
            {
                File.WriteAllText(settingsJson, JsonConvert.SerializeObject(settings, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Logger.LogToConsole(string.Format("Save settings error: {0}", ex.Message));
            }
        }

        public static void IncorrectSettingsMessage(string error, Settings settings, string settingsJson = "Settings.json")
        {
            settings.settingsFile = settingsJson;

            ConsoleColor color = ConsoleColor.Red;
            Logger.LogToConsole(string.Format("Unable to load settings file {0}: {1}", settingsJson, error), "ERROR", color);
            Logger.LogToConsole("Would you like to update the current JSON to the newest settings? Y/N", "ERROR", color);
            string key = Console.ReadKey().Key.ToString();
            if (key == "Y")
            {
                writeSettings(settings.settingsFile, GetGenericSettings(settings));
            }
            System.Environment.Exit(1);
        }

        public static Settings GetGenericSettings(Settings settings)
        {
            if (settings.allowedAddresses.Count == 0)
            {
                settings.allowedAddresses.Add("127.0.0.1");
                settings.allowedAddresses.Add("0.0.0.0");
            }
            settings.showEndpointInConsole = true;
            settings.identifyDevFee = true;
            settings.showRigStats = true;
            settings.colorizeConsole = true;
            settings.replaceWallet = true;
            settings.usePasswordAsRigName = false;
            settings.useWebSockServer = true;
            settings.donateDevFee = false;
            if (settings.percentToDonate == 0) settings.percentToDonate = 10;
            if (settings.proxyListenPort == 0) settings.proxyListenPort = 9000;
            //if (settings.remotePoolPort == 0) settings.remotePoolPort = 4444;
            if (settings.webSocketPort == 0) settings.webSocketPort = 9091;
            if (settings.rigStatsIntervalSeconds == 0) settings.rigStatsIntervalSeconds = 60;
            //if (string.IsNullOrEmpty(settings.remotePoolAddress)) settings.remotePoolAddress = "us1.ethermine.org";
            if (string.IsNullOrEmpty(settings.walletAddress)) settings.walletAddress = "0x3Ff3CF71689C7f2f8F5c1b7Fc41e030009ff7332";
            if (string.IsNullOrEmpty(settings.devFeeWalletAddress)) settings.devFeeWalletAddress = "";
            if (string.IsNullOrEmpty(settings.minedCoin)) settings.minedCoin = "VAP";

            if (settings.poolList.Count == 0)
            {
                settings.poolList.Add(new PoolItem("pool.vapory.org", 8008));
                settings.poolList.Add(new PoolItem("POOL.ADDRESS.OR.BLANK", 4444));
            } else
            {
                for (int i = 0; i < settings.poolList.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(settings.poolList[i].poolAddress))
                        settings.poolList[i].poolAddress = "POOL.ADDRESS.HERE";
                    if (settings.poolList[i].poolPort == 0)
                        settings.poolList[i].poolPort = 4444;
                }
            }

            return settings;
        }

        public static void ProcessArgs(string[] args, out Settings settings)
        {

            settings = new Settings();

            if (args.Length < 6 && args.Length > 1) //check if they're using command args
            {
                Logger.LogToConsole("Usage : MinerProxy.exe <JsonFile>");
                Logger.LogToConsole("MinerProxy.exe Vapormine.json");
                System.Environment.Exit(1);
            }
            else if (args.Length == 1)
            {
                LoadSettings(out settings, args[0]);    //first supplied argument should be a json file
                return;
            }
            else if (args.Length >= 2) //if they are, and the args match the 6 we're looking for..
            {
                Logger.LogToConsole("Command arguments are no longer accepted; pass a JSON file instead.");
                Console.Write("Press any key to exit..");
                Console.ReadKey();
                System.Environment.Exit(1);
            }
            else //there were no args, so we can check for a settings.json file
            {
                LoadSettings(out settings);
                return;
            }
        }

        public static bool ValidateCoin(string coin) //Pass the string of a coin, convert to uppercase, and return true if it's valid, else false
        {
            coin = coin.ToUpper();
            switch (coin)
            {
                case "ETC":
                case "VAP":
                    return true;

                case "SIA":
                case "SC":
                    return true;

                case "ZCASH":
                case "HUSH":
                case "ZEC":
                    return true;

                case "PASC":
                    return true;

                case "DCR":
                    return true;

                case "LBRY":
                    return true;

                case "UBIQ":
                case "UBQ":
                    return true;

                case "CRYPTONOTE":
                case "CRY":
                    return true;

                case "NICEHASH":
                    return true;

                case "XMR":
                    return true;

                case "TCP":
                    return true;

                default:
                    return false;
            }

        }
    }
}
