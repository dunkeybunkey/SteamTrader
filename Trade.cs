using System;
 using System.Collections.Generic;
+using Newtonsoft.Json;
 using SteamKit2;
 using SteamTrade.Exceptions;
+using Newtonsoft.Json.Linq;
+using SteamTrade.TradeWebAPI;
 
 namespace SteamTrade
 {
@@ -30,26 +33,25 @@ public partial class Trade
         List<ulong> steamMyOfferedItems;
 
         // Internal properties needed for Steam API.
-        string apiKey;
         int numEvents;
 
-        internal Trade (SteamID me, SteamID other, string sessionId, string token, string apiKey, Inventory myInventory, Inventory otherInventory)
+        private readonly TradeSession session;
+
+        internal Trade(SteamID me, SteamID other, string sessionId, string token, Inventory myInventory, Inventory otherInventory)
         {
             mySteamId = me;
             OtherSID = other;
-            this.sessionId = sessionId;
-            this.steamLogin = token;
-            this.apiKey = apiKey;
+            session = new TradeSession(sessionId, token, other, "440");
+
             this.eventList = new List<TradeEvent>();
 
-            OtherOfferedItems = new List<ulong> ();
-            myOfferedItems = new Dictionary<int, ulong> ();
-            steamMyOfferedItems = new List<ulong> ();
+            OtherOfferedItems = new List<ulong>();
+            myOfferedItems = new Dictionary<int, ulong>();
+            steamMyOfferedItems = new List<ulong>();
 
             OtherInventory = otherInventory;
             MyInventory = myInventory;
 
-            Init ();
         }
 
         #region Public Properties
@@ -71,6 +73,11 @@ public SteamID MySteamId
         public Inventory OtherInventory { get; private set; }
 
         /// <summary> 
+        /// Gets the private inventory of the other user. 
+        /// </summary>
+        public ForeignInventory OtherPrivateInventory { get; private set; }
+
+        /// <summary> 
         /// Gets the inventory of the bot.
         /// </summary>
         public Inventory MyInventory { get; private set; }
@@ -193,7 +200,7 @@ public bool TradeStarted
         /// </summary>
         public bool CancelTrade ()
         {
-            bool ok = CancelTradeWebCmd ();
+            bool ok = session.CancelTradeWebCmd ();
 
             if (!ok)
                 throw new TradeException ("The Web command to cancel the trade failed");
@@ -214,7 +221,7 @@ public bool AddItem (ulong itemid)
                 return false;
 
             var slot = NextTradeSlot ();
-            bool ok = AddItemWebCmd (itemid, slot);
+            bool ok = session.AddItemWebCmd (itemid, slot);
 
             if (!ok)
                 throw new TradeException ("The Web command to add the Item failed");
@@ -284,7 +291,7 @@ public bool RemoveItem (ulong itemid)
             if (!slot.HasValue)
                 return false;
 
-            bool ok = RemoveItemWebCmd (itemid, slot.Value);
+            bool ok = session.RemoveItemWebCmd(itemid, slot.Value);
 
             if (!ok)
                 throw new TradeException ("The web command to remove the item failed.");
@@ -370,7 +377,7 @@ public uint RemoveAllItems()
         /// </summary>
         public bool SendMessage (string msg)
         {
-            bool ok = SendMessageWebCmd (msg);
+            bool ok = session.SendMessageWebCmd(msg);
 
             if (!ok)
                 throw new TradeException ("The web command to send the trade message failed.");
@@ -386,7 +393,7 @@ public bool SetReady (bool ready)
             // testing
             ValidateLocalTradeItems ();
 
-            bool ok = SetReadyWebCmd (ready);
+            bool ok = session.SetReadyWebCmd(ready);
 
             if (!ok)
                 throw new TradeException ("The web command to set trade ready state failed.");
@@ -402,7 +409,7 @@ public bool AcceptTrade ()
         {
             ValidateLocalTradeItems ();
 
-            bool ok = AcceptTradeWebCmd ();
+            bool ok = session.AcceptTradeWebCmd();
 
             if (!ok)
                 throw new TradeException ("The web command to accept the trade failed.");
@@ -430,7 +437,7 @@ public bool Poll ()
                     OnAfterInit ();
             }
 
-            StatusObj status = GetStatus ();
+            TradeStatus status = session.GetStatus();
 
             if (status == null)
                 throw new TradeException ("The web command to get the trade status failed.");
@@ -452,136 +459,167 @@ public bool Poll ()
                 return otherDidSomething;
             }
 
+            if (status.newversion)
+            {
+                // handle item adding and removing
+                session.Version = status.version;
 
-            if (status.events != null)
+                TradeEvent trdEvent = status.GetLastEvent();
+                TradeEventType actionType = (TradeEventType) trdEvent.action;
+                HandleTradeVersionChange(status);
+                return true;
+            }
+            else if (status.version > session.Version)
+            {
+                // oh crap! we missed a version update abort so we don't get 
+                // scammed. if we could get what steam thinks what's in the 
+                // trade then this wouldn't be an issue. but we can only get 
+                // that when we see newversion == true
+                throw new TradeException("The trade version does not match. Aborting.");
+            }
+
+            var events = status.GetAllEvents();
+
+            foreach (var tradeEvent in events)
             {
-                foreach (TradeEvent trdEvent in status.events)
+                if (eventList.Contains(tradeEvent))
+                    continue;
+
+                //add event to processed list, as we are taking care of this event now
+                eventList.Add(tradeEvent);
+
+                bool isBot = tradeEvent.steamid == MySteamId.ConvertToUInt64().ToString();
+
+                // dont process if this is something the bot did
+                if (isBot)
+                    continue;
+
+                otherDidSomething = true;
+
+                /* Trade Action ID's
+                 * 0 = Add item (itemid = "assetid")
+                 * 1 = remove item (itemid = "assetid")
+                 * 2 = Toggle ready
+                 * 3 = Toggle not ready
+                 * 4 = ?
+                 * 5 = ? - maybe some sort of cancel
+                 * 6 = ?
+                 * 7 = Chat (message = "text")        */
+                switch ((TradeEventType) tradeEvent.action)
                 {
-                    if (!eventList.Contains(trdEvent))
-                    {
-                        eventList.Add(trdEvent);//add event to processed list, as we are taking care of this event now
-                        bool isBot = trdEvent.steamid == MySteamId.ConvertToUInt64().ToString();
-
-                        /*
-                            *
-                            * Trade Action ID's
-                            *
-                            * 0 = Add item (itemid = "assetid")
-                            * 1 = remove item (itemid = "assetid")
-                            * 2 = Toggle ready
-                            * 3 = Toggle not ready
-                            * 4
-                            * 5
-                            * 6
-                            * 7 = Chat (message = "text")
-                            *
-                            */
-                        ulong itemID;
-
-                        switch ((TradeEventType)trdEvent.action)
-                        {
-                            case TradeEventType.ItemAdded:
-                                itemID = (ulong)trdEvent.assetid;
-
-                                if (isBot)
-                                {
-                                    steamMyOfferedItems.Add(itemID);
-                                    ValidateSteamItemChanged(itemID, true);
-                                    Inventory.Item item = MyInventory.GetItem(itemID);
-                                    Schema.Item schemaItem = CurrentSchema.GetItem(item.Defindex);
-                                }
-                                else
-                                {
-                                    if (!OtherOfferedItems.Contains(itemID))
-                                    {
-                                        OtherOfferedItems.Add(itemID);
-                                        Inventory.Item item = OtherInventory.GetItem(itemID);
-                                        Schema.Item schemaItem = CurrentSchema.GetItem(item.Defindex);
-                                        OnUserAddItem(schemaItem, item);
-                                    }
-                                    else
-                                    {
-                                        Console.WriteLine("Duplicate item ID of " + itemID + " was detected; ignoring event.");
-                                    }
-                                }
-
-                                break;
-                            case TradeEventType.ItemRemoved:
-                                itemID = (ulong)trdEvent.assetid;
-
-                                if (isBot)
-                                {
-                                    steamMyOfferedItems.Remove(itemID);
-                                    ValidateSteamItemChanged(itemID, false);
-                                    Inventory.Item item = MyInventory.GetItem(itemID);
-                                    Schema.Item schemaItem = CurrentSchema.GetItem(item.Defindex);
-                                }
-                                else
-                                {
-                                    OtherOfferedItems.Remove(itemID);
-                                    Inventory.Item item = OtherInventory.GetItem(itemID);
-                                    Schema.Item schemaItem = CurrentSchema.GetItem(item.Defindex);
-                                    OnUserRemoveItem(schemaItem, item);
-                                }
-                                break;
-                            case TradeEventType.UserSetReady:
-                                if (!isBot)
-                                {
-                                    otherIsReady = true;
-                                    OnUserSetReady(true);
-                                }
-                                break;
-                            case TradeEventType.UserSetUnReady:
-                                if (!isBot)
-                                {
-                                    otherIsReady = false;
-                                    OnUserSetReady(false);
-                                }
-                                break;
-                            case TradeEventType.UserAccept:
-                                if (!isBot)
-                                {
-                                    OnUserAccept();
-                                }
-                                break;
-                            case TradeEventType.UserChat:
-                                if (!isBot)
-                                {
-                                    OnMessage(trdEvent.text);
-                                }
-                                break;
-                            default:
-                                // Todo: add an OnWarning or similar event
-                                if (OnError != null)
-                                    OnError("Unknown Event ID: " + trdEvent.action);
-                                break;
-                        }
-
-                        if (!isBot)
-                            otherDidSomething = true;
-                    }//if (!eventList.Contains(trdEvent))
-                }// foreach (TradeEvent trdEvent in status.events)
-            }//if (status.events != null)
+                    case TradeEventType.ItemAdded:
+                        FireOnUserAddItem(tradeEvent);
+                        break;
+                    case TradeEventType.ItemRemoved:
+                        FireOnUserRemoveItem(tradeEvent);
+                        break;
+                    case TradeEventType.UserSetReady:
+                        otherIsReady = true;
+                        OnUserSetReady(true);
+                        break;
+                    case TradeEventType.UserSetUnReady:
+                        otherIsReady = false;
+                        OnUserSetReady(false);
+                        break;
+                    case TradeEventType.UserAccept:
+                        OnUserAccept();
+                        break;
+                    case TradeEventType.UserChat:
+                        OnMessage(tradeEvent.text);
+                        break;
+                    default:
+                        // Todo: add an OnWarning or similar event
+                        if (OnError != null)
+                            OnError("Unknown Event ID: " + tradeEvent.action);
+                        break;
+                }
+            }
 
             // Update Local Variables
             if (status.them != null)
             {
-                otherIsReady = status.them.ready == 1 ? true : false;
-                meIsReady = status.me.ready == 1 ? true : false;
+                otherIsReady = status.them.ready == 1;
+                meIsReady = status.me.ready == 1;
             }
 
-            // Update version
-            if (status.newversion)
+            if (status.logpos != 0)
             {
-                Version = status.version;
+                session.LogPos = status.logpos;
             }
 
-            if (status.logpos != 0)
+            return otherDidSomething;
+        }
+
+        void HandleTradeVersionChange(TradeStatus status)
+        {
+            CopyNewAssets(OtherOfferedItems, status.them.GetAssets());
+
+            CopyNewAssets(steamMyOfferedItems, status.me.GetAssets());
+        }
+
+        private void CopyNewAssets(List<ulong> dest, TradeUserAssets[] assetList)
+        {
+            if (assetList == null) 
+                return;
+
+            //Console.WriteLine("clearing dest");
+            dest.Clear();
+
+            foreach (var asset in assetList)
             {
-                LogPos = status.logpos;
+                dest.Add(asset.assetid);
+                //Console.WriteLine(asset.assetid);
             }
+        }
 
-            return otherDidSomething;
+        private void FireOnUserAddItem(TradeEvent tradeEvent)
+        {
+            ulong itemID = tradeEvent.assetid;
+
+            if (OtherInventory != null)
+            {
+                Inventory.Item item = OtherInventory.GetItem(itemID);
+                Schema.Item schemaItem = CurrentSchema.GetItem(item.Defindex);
+                OnUserAddItem(schemaItem, item);
+            }
+            else
+            {
+                var schemaItem = GetItemFromPrivateBp(tradeEvent, itemID);
+                OnUserAddItem(schemaItem, null);
+                // todo: figure out what to send in with Inventory item.....
+            }
+        }
+
+        private Schema.Item GetItemFromPrivateBp(TradeEvent tradeEvent, ulong itemID)
+        {
+            if (OtherPrivateInventory == null)
+            {
+                // get the foreign inventory
+                var f = session.GetForiegnInventory(OtherSID, tradeEvent.contextid);
+                OtherPrivateInventory = new ForeignInventory(f);
+            }
+
+            ushort defindex = OtherPrivateInventory.GetDefIndex(itemID);
+
+            Schema.Item schemaItem = CurrentSchema.GetItem(defindex);
+            return schemaItem;
+        }
+
+        private void FireOnUserRemoveItem(TradeEvent tradeEvent)
+        {
+            ulong itemID = (ulong) tradeEvent.assetid;
+
+            if (OtherInventory != null)
+            {
+                Inventory.Item item = OtherInventory.GetItem(itemID);
+                Schema.Item schemaItem = CurrentSchema.GetItem(item.Defindex);
+                OnUserRemoveItem(schemaItem, item);
+            }
+            else
+            {
+                var schemaItem = GetItemFromPrivateBp(tradeEvent, itemID);
+                OnUserRemoveItem(schemaItem, null);
+            }
         }
 
         internal void FireOnCloseEvent()
