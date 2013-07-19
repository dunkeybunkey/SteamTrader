 using System.Collections.Specialized;
 using System.Net;
 using Newtonsoft.Json;
+using SteamKit2;
 
-namespace SteamTrade
+namespace SteamTrade.TradeWebAPI
 {
     /// <summary>
-    /// This class handles the web-based interaction for Steam trades.
+    /// This class provides the interface into the Web API for trading on the
+    /// Steam network.
     /// </summary>
-    public partial class Trade
+    public class TradeSession
     {
         static string SteamCommunityDomain = "steamcommunity.com";
         static string SteamTradeUrl = "http://steamcommunity.com/trade/{0}/";
 
-        string sessionId;
         string sessionIdEsc;
         string baseTradeURL;
-        string steamLogin;
         CookieContainer cookies;
-        
 
+        readonly string steamLogin;
+        readonly string sessionId;
+        readonly SteamID OtherSID;
+        readonly string appIdValue;
+
+        /// <summary>
+        /// Initializes a new instance of the <see cref="TradeSession"/> class.
+        /// </summary>
+        /// <param name="sessionId">The session id.</param>
+        /// <param name="steamLogin">The current steam login.</param>
+        /// <param name="otherSid">The Steam id of the other trading partner.</param>
+        /// <param name="appId">The Steam app id. Ex. "440" for TF2</param>
+        public TradeSession(string sessionId, string steamLogin, SteamID otherSid, string appId)
+        {
+            this.sessionId = sessionId;
+            this.steamLogin = steamLogin;
+            OtherSID = otherSid;
+            appIdValue = appId;
+
+            Init();
+        }
+
+        #region Trade status properties
+        
+        /// <summary>
+        /// Gets the LogPos number of the current trade.
+        /// </summary>
+        /// <remarks>This is not automatically updated by this class.</remarks>
         internal int LogPos { get; set; }
 
+        /// <summary>
+        /// Gets the version number of the current trade. This increments on
+        /// every item added or removed from a trade.
+        /// </summary>
+        /// <remarks>This is not automatically updated by this class.</remarks>
         internal int Version { get; set; }
 
-        StatusObj GetStatus ()
+        #endregion Trade status properties
+
+        #region Trade Web API command methods
+
+        /// <summary>
+        /// Gets the trade status.
+        /// </summary>
+        /// <returns>A deserialized JSON object into <see cref="TradeStatus"/></returns>
+        /// <remarks>
+        /// This is the main polling method for trading and must be done at a 
+        /// periodic rate (probably around 1 second).
+        /// </remarks>
+        internal TradeStatus GetStatus()
         {
             var data = new NameValueCollection ();
 
@@ -33,15 +77,41 @@ StatusObj GetStatus ()
             data.Add ("version", "" + Version);
             
             string response = Fetch (baseTradeURL + "tradestatus", "POST", data);
-            return JsonConvert.DeserializeObject<StatusObj> (response);
+
+            return JsonConvert.DeserializeObject<TradeStatus> (response);
         }
 
-        #region Trade Web command methods
+
+        /// <summary>
+        /// Gets the foriegn inventory.
+        /// </summary>
+        /// <param name="otherId">The other id.</param>
+        /// <param name="contextId">The current trade context id.</param>
+        /// <returns>A dynamic JSON object.</returns>
+        internal dynamic GetForiegnInventory(SteamID otherId, int contextId)
+        {
+            var data = new NameValueCollection();
+
+            data.Add("sessionid", sessionIdEsc);
+            data.Add("steamid", otherId.ConvertToUInt64().ToString());
+            data.Add("appid", appIdValue);
+            data.Add("contextid", contextId.ToString());
+
+            try
+            {
+                string response = Fetch(baseTradeURL + "foreigninventory", "POST", data);
+                return JsonConvert.DeserializeObject(response);
+            }
+            catch (Exception)
+            {
+                return JsonConvert.DeserializeObject("{\"success\":\"false\"}");
+            }
+        }
 
         /// <summary>
         /// Sends a message to the user over the trade chat.
         /// </summary>
-        bool SendMessageWebCmd (string msg)
+        internal bool SendMessageWebCmd(string msg)
         {
             var data = new NameValueCollection ();
             data.Add ("sessionid", sessionIdEsc);
@@ -70,12 +140,12 @@ bool SendMessageWebCmd (string msg)
         /// Returns false if the item doesn't exist in the Bot's inventory,
         /// and returns true if it appears the item was added.
         /// </returns>
-        bool AddItemWebCmd (ulong itemid, int slot)
+        internal bool AddItemWebCmd(ulong itemid, int slot)
         {
             var data = new NameValueCollection ();
 
             data.Add ("sessionid", sessionIdEsc);
-            data.Add ("appid", "440");
+            data.Add ("appid", appIdValue);
             data.Add ("contextid", "2");
             data.Add ("itemid", "" + itemid);
             data.Add ("slot", "" + slot);
@@ -97,12 +167,12 @@ bool AddItemWebCmd (ulong itemid, int slot)
         /// Returns false if the item isn't in the offered items, or
         /// true if it appears it succeeded.
         /// </summary>
-        bool RemoveItemWebCmd (ulong itemid, int slot)
+        internal bool RemoveItemWebCmd(ulong itemid, int slot)
         {
             var data = new NameValueCollection ();
 
             data.Add ("sessionid", sessionIdEsc);
-            data.Add ("appid", "440");
+            data.Add ("appid", appIdValue);
             data.Add ("contextid", "2");
             data.Add ("itemid", "" + itemid);
             data.Add ("slot", "" + slot);
@@ -122,7 +192,7 @@ bool RemoveItemWebCmd (ulong itemid, int slot)
         /// <summary>
         /// Sets the bot to a ready status.
         /// </summary>
-        bool SetReadyWebCmd (bool ready)
+        internal bool SetReadyWebCmd(bool ready)
         {
             var data = new NameValueCollection ();
             data.Add ("sessionid", sessionIdEsc);
@@ -145,7 +215,7 @@ bool SetReadyWebCmd (bool ready)
         /// Accepts the trade from the user.  Returns a deserialized
         /// JSON object.
         /// </summary>
-        bool AcceptTradeWebCmd ()
+        internal bool AcceptTradeWebCmd()
         {
             var data = new NameValueCollection ();
 
@@ -167,7 +237,7 @@ bool AcceptTradeWebCmd ()
         /// <summary>
         /// Cancel the trade.  This calls the OnClose handler, as well.
         /// </summary>
-        bool CancelTradeWebCmd ()
+        internal bool CancelTradeWebCmd ()
         {
             var data = new NameValueCollection ();
 
@@ -185,14 +255,14 @@ bool CancelTradeWebCmd ()
             return true;
         }
 
-        #endregion Trade Web command methods
+        #endregion Trade Web API command methods
         
         string Fetch (string url, string method, NameValueCollection data = null)
         {
             return SteamWeb.Fetch (url, method, data, cookies);
         }
 
-        void Init()
+        private void Init()
         {
             sessionIdEsc = Uri.UnescapeDataString(sessionId);
 
@@ -202,87 +272,8 @@ void Init()
             cookies.Add (new Cookie ("sessionid", sessionId, String.Empty, SteamCommunityDomain));
             cookies.Add (new Cookie ("steamLogin", steamLogin, String.Empty, SteamCommunityDomain));
 
-            baseTradeURL = String.Format (SteamTradeUrl, OtherSID.ConvertToUInt64 ());
-        }
-
-        public class StatusObj
-        {
-            public string error { get; set; }
-            
-            public bool newversion { get; set; }
-            
-            public bool success { get; set; }
-            
-            public long trade_status { get; set; }
-            
-            public int version { get; set; }
-            
-            public int logpos { get; set; }
-            
-            public TradeUserObj me { get; set; }
-            
-            public TradeUserObj them { get; set; }
-            
-            public TradeEvent[] events { get; set; }
-        }
-
-        public class TradeEvent : IEquatable<TradeEvent>
-        {
-            public string steamid { get; set; }
-            
-            public int action { get; set; }
-            
-            public ulong timestamp { get; set; }
-            
-            public int appid { get; set; }
-            
-            public string text { get; set; }
-            
-            public int contextid { get; set; }
-            
-            public ulong assetid { get; set; }
-
-            /// <summary>
-            /// Determins if the TradeEvent is equal to another.
-            /// </summary>
-            /// <param name="other">TradeEvent to compare to</param>
-            /// <returns>True if equal, false if not</returns>
-            public bool Equals(TradeEvent other)
-            {
-                if (this.steamid == other.steamid && this.action == other.action
-                    && this.timestamp == other.timestamp && this.appid == other.appid
-                    && this.text == other.text && this.contextid == other.contextid
-                    && this.assetid == other.assetid)
-                {
-                    return true;
-                }
-                else
-                {
-                    return false;
-                }
-            }
-        }
-        
-        public class TradeUserObj
-        {
-            public int ready { get; set; }
-            
-            public int confirmed { get; set; }
-            
-            public int sec_since_touch { get; set; }
-        }
-
-        public enum TradeEventType : int
-        {
-            ItemAdded = 0,
-            ItemRemoved = 1,
-            UserSetReady = 2,
-            UserSetUnReady = 3,
-            UserAccept = 4,
-            UserChat = 7
+            baseTradeURL = String.Format (SteamTradeUrl, OtherSID.ConvertToUInt64());
         }
     }
-
-
 }
 
