 {
                     tradeManager.InitializeTrade(SteamUser.SteamID, callback.OtherClient);
                 }
-                catch 
-                {
-                    SteamFriends.SendChatMessage(callback.OtherClient, 
-                                                 EChatEntryType.ChatMsg,
-                                                 "Trade declined. Could not correctly fetch your backpack.");
-                    
-                    SteamTrade.RespondToTrade (callback.TradeID, false);
+                catch (WebException we)
+                {                 
+                    SteamFriends.SendChatMessage(callback.OtherClient,
+                             EChatEntryType.ChatMsg,
+                             "Trade error: " + we.Message);
+
+                    SteamTrade.RespondToTrade(callback.TradeID, false);
                     return;
                 }
-
-                if (tradeManager.OtherInventory.IsPrivate)
+                catch (Exception)
                 {
-                    SteamFriends.SendChatMessage(callback.OtherClient, 
-                                                 EChatEntryType.ChatMsg,
-                                                 "Trade declined. Your backpack cannot be private.");
+                    SteamFriends.SendChatMessage(callback.OtherClient,
+                             EChatEntryType.ChatMsg,
+                             "Trade declined. Could not correctly fetch your backpack.");
 
-                    SteamTrade.RespondToTrade (callback.TradeID, false);
+                    SteamTrade.RespondToTrade(callback.TradeID, false);
                     return;
                 }
 
+                //if (tradeManager.OtherInventory.IsPrivate)
+                //{
+                //    SteamFriends.SendChatMessage(callback.OtherClient, 
+                //                                 EChatEntryType.ChatMsg,
+                //                                 "Trade declined. Your backpack cannot be private.");
+
+                //    SteamTrade.RespondToTrade (callback.TradeID, false);
+                //    return;
+                //}
+
                 if (CurrentTrade == null && GetUserHandler (callback.OtherClient).OnTradeRequest ())
                     SteamTrade.RespondToTrade (callback.TradeID, true);
                 else
