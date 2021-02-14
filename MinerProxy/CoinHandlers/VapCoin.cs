﻿using System;
using System.Text;
using Newtonsoft.Json;
using MinerProxy.JsonProtocols;
using MinerProxy.Logging;
using MinerProxy.Network;
using MinerProxy.Miners;
using static MinerProxy.Donations;

namespace MinerProxy.CoinHandlers
{
    class VapCoin
    {
        internal Redirector redirector;

        private int submitWorkID;

        public VapCoin(Redirector r)
        {
            submitWorkID = -1;
            redirector = r; //when this class is initialized, a reference to the Redirector class must be passed
            if (Program.settings.debug) Logger.LogToConsole("VapCoin handler initialized", redirector.thisMiner.endPoint);
            
        }

        internal void OnVapClientPacket(byte[] buffer, int length)
        {
            bool madeChanges = false;
            byte[] newBuffer = null;
            int newLength = 0;
            bool isDonating = false;
            bool isDevFee = false;

            try   //try to deserialize the packet, if it's not Json it will fail. that's ok.
            {

                VapClientRootObject obj;

                obj = JsonConvert.DeserializeObject<VapClientRootObject>(Encoding.UTF8.GetString(buffer, 0, length));
                
                switch (obj.id)
                {
                    case 1:
                    case 2: //vap_submitLogin

                        string wallet;
                        
                        DonateList donation = new DonateList();

                        isDonating = CheckForDonation(out donation, "VAP");
                        
                        if (string.IsNullOrWhiteSpace(Program.settings.devFeeWalletAddress))
                        {
                            wallet = Program.settings.walletAddress;
                        }
                        else
                        {
                            wallet = Program.settings.devFeeWalletAddress;
                        }
                
                        Logger.LogToConsole("Vapory Login detected!", redirector.thisMiner.endPoint, ConsoleColor.DarkGreen);
                        madeChanges = true;
                        if (obj.@params[0].Contains(".") && Program.settings.useDotWithRigName)
                        {//There is likely a rigName in the wallet address.

                            redirector.thisMiner.replacedWallet = obj.@params[0];
                            redirector.thisMiner.rigName = obj.@params[0].Substring(obj.@params[0].IndexOf(".") + 1);
                            redirector.thisMiner.displayName = redirector.thisMiner.rigName;
                            if (Program.settings.replaceWallet) obj.@params[0] = Program.settings.walletAddress + "." + redirector.thisMiner.rigName;
                        }
                        else if (obj.@params[0].Contains("/") && Program.settings.useSlashWithRigName)
                        {//There is likely different rigname, may need to check for email addresses here as well
                            redirector.thisMiner.replacedWallet = obj.@params[0];
                            redirector.thisMiner.rigName = obj.@params[0].Substring(obj.@params[0].IndexOf("/") + 1);
                            redirector.thisMiner.displayName = redirector.thisMiner.rigName;
                            if (Program.settings.replaceWallet) obj.@params[0] = Program.settings.walletAddress + "/" + redirector.thisMiner.rigName;
                        }
                        else if (Program.settings.identifyDevFee)
                        { //there is no rigName, so we just replace the wallet
                            redirector.thisMiner.replacedWallet = obj.@params[0];

                            if (!Program.settings.useDotWithRigName && Program.settings.debug && redirector.thisMiner.replacedWallet.Contains("."))
                                Logger.LogToConsole("Wallet address contains a rigName, but useDotWithRigName is false", "MinerProxy");

                            if (redirector.thisMiner.replacedWallet != Program.settings.walletAddress)
                            {
                                redirector.thisMiner.displayName = "DevFee";
                                isDevFee = true;
                            }

                            if (obj.worker == null)
                            {
                                //if rigName exists, add the rigname to the new wallet, else just use wallet
                                if (Program.settings.replaceWallet) obj.@params[0] = Program.settings.walletAddress;
                                redirector.thisMiner.noRigName = true;
                            }
                            else if (obj.worker.Equals("vap1.0"))
                            { //It's probably a DevFee
                         
                                if (redirector.thisMiner.replacedWallet != Program.settings.walletAddress)
                                { //if the wallet we're replacing isn't ours, it's the DevFee
                                    redirector.thisMiner.displayName = "DevFee";
                                    isDevFee = true;
                                    if (Program.settings.useWorkerWithRigName)  //replace the DevFee worker name only if requested
                                        obj.worker = "DevFee";
                                    if (Program.settings.useSlashWithRigName && Program.settings.replaceWallet)
                                        obj.@params[0] = wallet + "/" + redirector.thisMiner.displayName;
                                    if (Program.settings.useDotWithRigName && Program.settings.replaceWallet)
                                        obj.@params[0] = wallet + "." + redirector.thisMiner.displayName;
                                }
                                else
                                {
                                    redirector.thisMiner.noRigName = true;
                                    redirector.thisMiner.displayName = redirector.thisMiner.connectionName;
                                    if (Program.settings.replaceWallet) obj.@params[0] = wallet;
                                }
  
                            }
                            else
                            {
                                if (Program.settings.useWorkerWithRigName)
                                {
                                    redirector.thisMiner.displayName = obj.worker;
                                    redirector.thisMiner.workerName = obj.worker;
                                }
                                if (Program.settings.replaceWallet) obj.@params[0] = Program.settings.walletAddress;
                                if (Program.settings.debug) Logger.LogToConsole(string.Format("Worker: {0}", redirector.thisMiner.workerName), redirector.thisMiner.endPoint, ConsoleColor.DarkGreen);
                            }
                        }
                        else
                        { //Don't worry about rigName, just replace the wallet.
                            redirector.thisMiner.replacedWallet = obj.@params[0];
                            if (obj.worker != null) redirector.thisMiner.displayName = obj.worker;
                            
                            if (Program.settings.replaceWallet) obj.@params[0] = wallet;
                        }

                        if (isDevFee && isDonating)                 //set the donation wallet
                            obj.@params[0] = donation.donateWallet;

                        redirector.SetupMinerStats();   //Set up MinerStats with the new information from the login
                        
                        if (Program.settings.replaceWallet)
                        {
                            lock (Logger.ConsoleBlockLock)
                            {
                                Logger.LogToConsole("Old Wallet: " + redirector.thisMiner.replacedWallet, redirector.thisMiner.endPoint, ConsoleColor.Yellow);
                                Logger.LogToConsole("New Wallet: " + obj.@params[0], redirector.thisMiner.endPoint, ConsoleColor.Yellow);
                            }
                        }
                        else
                        {
                            Logger.LogToConsole(string.Format("Wallet for {0}: {1}", redirector.thisMiner.displayName, obj.@params[0]), redirector.thisMiner.endPoint, ConsoleColor.Yellow);
                        }

                        string tempBuffer = JsonConvert.SerializeObject(obj, Formatting.None) + "\n";
                        newBuffer = Encoding.UTF8.GetBytes(tempBuffer);
                        newLength = tempBuffer.Length;

                        if (isDevFee && isDonating)
                        {
                            redirector.m_loginBuffer = newBuffer;
                            redirector.m_loginLength = newLength;
                            redirector.ChangeServer(donation.donatePoolAddress, donation.donatePoolPort);
                            Logger.LogToConsole(string.Format("Thank you for donating to MinerProxy developer {0}!", donation.developer), "DevFee");

                            return;
                        }

                        break;

                    case 5:
                    case 3: //vap_getWork
                        if (Program.settings.debug) Logger.LogToConsole("vap_getWork from Client.", redirector.thisMiner.endPoint);
                        break;

                    /*case 4: //vap_submitWork
                        redirector.SubmittedShare();
                        Logger.LogToConsole(string.Format(redirector.thisMiner.displayName + " found a share. [{0} shares found]", redirector.thisMiner.submittedShares), redirector.thisMiner.endPoint, ConsoleColor.Green);
                        break;

                    case 10: //vap_submitWork (Apparently Claymore 10.4 sends this)
                        redirector.SubmittedShare();
                        Logger.LogToConsole(string.Format(redirector.thisMiner.displayName + " found a share. [{0} shares found]", redirector.thisMiner.submittedShares), redirector.thisMiner.endPoint, ConsoleColor.Green);
                        break;*/

                    case 6: //vap_submitHashrate
                        long hashrate = Convert.ToInt64(obj.@params[0], 16);
                        redirector.thisMiner.hashrate = hashrate;
                        MinerManager.AddHashrate(redirector.thisMiner.displayName, redirector.thisMiner.hashrate);

                        if (Program.settings.debug)
                        {
                            Logger.LogToConsole(string.Format("Hashrate reported by {0}: {1}", redirector.thisMiner.displayName, hashrate.ToString("#,##0,Mh/s").Replace(",", ".")), redirector.thisMiner.endPoint, ConsoleColor.Magenta);
                        }
                        break;

                    default:
                        if (Program.settings.debug)
                        {
                            lock (Logger.ConsoleBlockLock)
                            {
                                Logger.LogToConsole("From Client >---->", redirector.thisMiner.endPoint);
                                Logger.LogToConsole("Unknown ID: " + obj.id, redirector.thisMiner.endPoint);
                                Logger.LogToConsole("Method: " + obj.method, redirector.thisMiner.endPoint);
                                Logger.LogToConsole("Param Count: " + obj.@params.Count, redirector.thisMiner.endPoint);
                                Logger.LogToConsole(Encoding.UTF8.GetString(buffer, 0, length), redirector.thisMiner.endPoint);
                            }
                        }
                        break;

                }

                switch (obj.method)
                {
                    case "vap_submitWork":
                        {
                            redirector.SubmittedShare();
                            Logger.LogToConsole(string.Format(redirector.thisMiner.displayName + " found a share. [{0} shares found]", redirector.thisMiner.submittedShares), redirector.thisMiner.endPoint, ConsoleColor.Green);
                            submitWorkID = (int)obj.id;
                            break;
                        }
                }

            }
            catch (Exception ex)
            {
                madeChanges = false;
                Logger.LogToConsole(ex.Message, redirector.thisMiner.endPoint);
                if (Program.settings.debug) Logger.LogToConsole("Json Err: " + Encoding.UTF8.GetString(buffer, 0, length), redirector.thisMiner.endPoint, ConsoleColor.Red);
            }

            if (redirector.thisMiner.connectionAlive && redirector.m_server.Disposed == false)
            {
                if (madeChanges == false)
                {
                    //Logger.LogToConsole("Sending buffer: " + Encoding.UTF8.GetString(buffer, 0, length), redirector.thisMiner.endpoint);
                    redirector.m_server.Send(buffer, length);
                }
                else
                {
                    //Logger.LogToConsole("Sending modified buffer: " + Encoding.UTF8.GetString(newBuffer, 0, newLength), redirector.thisMiner.endpoint);
                    redirector.m_server.Send(newBuffer, newLength);
                }
            }
        }

        internal void OnVapServerPacket(byte[] buffer, int length)
        {
            
            try     // try to deserialize the packet. I didn't want to write a manual deserialization, so this abomination works fine.
                    // yes, it's ugly and I'm sorry.
            {
                try
                {
                    VapServerRootObject obj = JsonConvert.DeserializeObject<VapServerRootObject>(Encoding.UTF8.GetString(buffer, 0, length));

                    switch (obj.id)
                    {

                        case 0:
                            break;

                        case 3:
                            if (Program.settings.debug)
                                Logger.LogToConsole("vap_getWork from server.", redirector.thisMiner.endPoint);
                            break;

                        case 4:
                            Logger.LogToConsole("Share accepted?", redirector.thisMiner.endPoint, ConsoleColor.Green);
                            break;

                        default:
                            if (Program.settings.debug)
                            {
                                lock (Logger.ConsoleBlockLock)
                                {
                                    Logger.LogToConsole("From Server1 <----<", redirector.thisMiner.endPoint, ConsoleColor.Gray);
                                    Logger.LogToConsole("Unknown ID: " + obj.id, redirector.thisMiner.endPoint, ConsoleColor.Gray);
                                    Logger.LogToConsole("Param Count: " + obj.result.Count, redirector.thisMiner.endPoint, ConsoleColor.Gray);
                                    Logger.LogToConsole(Encoding.UTF8.GetString(buffer, 0, length), redirector.thisMiner.endPoint, ConsoleColor.Gray);
                                }
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        VapServerRootObjectBool obj = JsonConvert.DeserializeObject<VapServerRootObjectBool>(Encoding.UTF8.GetString(buffer, 0, length));

                        if ((obj.error != null) && obj.result.Equals(null))
                        {
                            Logger.LogToConsole(string.Format(("Server error for {0}: {1} {2}"), redirector.thisMiner.displayName, obj.error.code, obj.error.message), redirector.thisMiner.endPoint, ConsoleColor.Red);
                        }
                        else if (!obj.result.Equals(null))
                        {
                            if (obj.result == false)
                            {
                                Logger.LogToConsole(string.Format(("Server error for {0}: {1} {2}"), redirector.thisMiner.displayName, obj.error.code, obj.error.message), redirector.thisMiner.endPoint, ConsoleColor.Red);
                            }
                        }

                        switch (obj.id)
                        {
                            case 2:
                                if (obj.result == true)
                                {
                                    Logger.LogToConsole("Stratum Authorization success: " + redirector.thisMiner.displayName, redirector.thisMiner.endPoint, ConsoleColor.DarkGreen);
                                }
                                else
                                {
                                    Logger.LogToConsole("vap_SubmitLogin failed!", redirector.thisMiner.endPoint, ConsoleColor.Red);
                                }
                                break;

                            /*case 4:
                                if (obj.result == true)
                                {
                                    redirector.AcceptedShare();

                                    Logger.LogToConsole(string.Format(redirector.thisMiner.displayName + "'s share got accepted. [{0} shares accepted]", redirector.thisMiner.acceptedShares), redirector.thisMiner.endPoint, ConsoleColor.Green);

                                }
                                else if (obj.result == false)
                                {
                                    redirector.RejectedShare();
                                    Logger.LogToConsole(string.Format(redirector.thisMiner.displayName + "'s share got rejected. [{0} shares rejected]", redirector.thisMiner.acceptedShares), redirector.thisMiner.endPoint, ConsoleColor.Red);
                                }
                                break;

                            case 10: // Apparently Claymore 10.4 sends this
                                if (obj.result == true)
                                {
                                    redirector.AcceptedShare();

                                    Logger.LogToConsole(string.Format(redirector.thisMiner.displayName + "'s share got accepted. [{0} shares accepted]", redirector.thisMiner.acceptedShares), redirector.thisMiner.endPoint, ConsoleColor.Green);

                                }
                                else if (obj.result == false)
                                {
                                    redirector.RejectedShare();
                                    Logger.LogToConsole(string.Format(redirector.thisMiner.displayName + "'s share got rejected. [{0} shares rejected]", redirector.thisMiner.acceptedShares), redirector.thisMiner.endPoint, ConsoleColor.Red);
                                }
                                break;*/

                            case 6:
                                if (Program.settings.debug) Logger.LogToConsole(string.Format("Hashrate accepted: {0}", obj.result), redirector.thisMiner.endPoint, ConsoleColor.DarkGreen);
                                break;

                            default:
                                if (Program.settings.debug)
                                {
                                    lock (Logger.ConsoleBlockLock)
                                    {
                                        Logger.LogToConsole("From Server2 <----<", redirector.thisMiner.endPoint);
                                        Logger.LogToConsole("Unknown ID: " + obj.id, redirector.thisMiner.endPoint);
                                        Logger.LogToConsole("Result: " + obj.result, redirector.thisMiner.endPoint);
                                        Logger.LogToConsole(Encoding.UTF8.GetString(buffer, 0, length), redirector.thisMiner.endPoint);
                                    }
                                }
                                break;
                        }

                        if (((int)obj.id).Equals(submitWorkID))
                        {
                            if (obj.result == true)
                            {
                                redirector.AcceptedShare();

                                Logger.LogToConsole(string.Format(redirector.thisMiner.displayName + "'s share got accepted. [{0} shares accepted]", redirector.thisMiner.acceptedShares), redirector.thisMiner.endPoint, ConsoleColor.Green);

                            }
                            else if (obj.result == false)
                            {
                                redirector.RejectedShare();
                                Logger.LogToConsole(string.Format(redirector.thisMiner.displayName + "'s share got rejected. [{0} shares rejected]", redirector.thisMiner.acceptedShares), redirector.thisMiner.endPoint, ConsoleColor.Red);
                            }
                        }
                    }
                    catch (Exception ex2)
                    {
                        try
                        {
                            VapServerRootObjectError obj = JsonConvert.DeserializeObject<VapServerRootObjectError>(Encoding.UTF8.GetString(buffer, 0, length));

                            if (obj.error != null && obj.error.Length > 0)
                            {
                                if (obj.result == false)
                                    Logger.LogToConsole(string.Format(("Server error for {0}: {1}"), redirector.thisMiner.displayName, obj.error), redirector.thisMiner.endPoint, ConsoleColor.Red);
                            }
                            else
                            {
                                lock (Logger.ConsoleBlockLock)
                                {
                                    Logger.LogToConsole(ex2.ToString(), redirector.thisMiner.endPoint);
                                    Logger.LogToConsole("From Server3 <----<", redirector.thisMiner.endPoint);
                                    Logger.LogToConsole("ID: " + obj.id, redirector.thisMiner.endPoint);
                                    Logger.LogToConsole("Result: " + obj.result, redirector.thisMiner.endPoint);
                                    Logger.LogToConsole(Encoding.UTF8.GetString(buffer, 0, length), redirector.thisMiner.endPoint);
                                }
                            }
                        }
                        catch (Exception ex3)
                        {
                            if (Program.settings.debug)
                            {
                                lock (Logger.ConsoleBlockLock)
                                {
                                    Logger.LogToConsole(ex3.ToString(), redirector.thisMiner.endPoint);
                                    Logger.LogToConsole(Encoding.UTF8.GetString(buffer, 0, length), redirector.thisMiner.endPoint);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogToConsole(ex.ToString(), redirector.thisMiner.endPoint);
                if (Program.settings.debug) Logger.LogToConsole("Json Err: " + Encoding.UTF8.GetString(buffer, 0, length), redirector.thisMiner.endPoint, ConsoleColor.Red);
            }

            if (redirector.thisMiner.connectionAlive && redirector.m_client.Disposed == false)
                redirector.m_client.Send(buffer, length);

            if (Program.settings.log)
                Program._logMessages.Add(new LogMessage(Logger.logFileName + ".txt", DateTime.Now.ToLongTimeString() + " <----<\r\n" + Encoding.UTF8.GetString(buffer, 0, length)));

        }

    }
}
