using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Speech.Recognition;

namespace JarvisDesk {
 public partial class Desk {
  ChatGptClient chatClient;
  Border chatDrawer;
  TextBlock chatStatus, chatAccount;
  TextBox chatTranscript, chatInput;
  ComboBox chatModels;
  Expander chatSettings;
  CheckBox readChatAloud;
  Button chatConnect, chatSend, chatVoice, chatCancel, chatRefresh, chatDisconnect, chatClear;
  readonly List<ChatMessage> chatMessages = new List<ChatMessage>();
  readonly CancellationTokenSource chatLifetime = new CancellationTokenSource();
  CancellationTokenSource chatOperation;
  bool chatBusy, refreshingModels, questionListening, resumeLaunchListening;
  bool chatScrollPending;
  System.Windows.Threading.DispatcherTimer chatPreviewTimer;
  StringBuilder chatPreviewText;
  string chatPreviewPrefix;
  DateTime questionUntil = DateTime.MinValue;
  string ChatHistoryPath { get { return System.IO.Path.Combine(dataDir,"chatgpt-history.protected"); } }
  void InitializeChat() {
   chatClient = new ChatGptClient(dataDir);
   try { if(System.IO.File.Exists(ChatHistoryPath)) { var saved = ChatGptClient.ReadProtected<List<ChatMessage>>(ChatHistoryPath); if(saved != null && saved.Count<=40 && saved.All(m=>m != null && (m.Role=="user"||m.Role=="assistant") && !String.IsNullOrWhiteSpace(m.Content)) && saved.Sum(m=>m.Content.Length)<=32000) chatMessages.AddRange(saved); } } catch { Log("Saved chat history could not be loaded. Start a new chat if needed."); }
   ApplyChatName(); UpdateChatAccount(); chatSettings.IsExpanded=!chatClient.Connected;
   Loaded += delegate { if(chatClient.Connected && chatModels.Items.Count==0 && !chatBusy) RefreshChatModels(); };
  }
  Button ChatControl(string title, double width, Action action) {
   var button=Action(title,action); button.Width=width; button.Padding=new Thickness(4,5,4,5); button.FontSize=10; button.Margin=new Thickness(0,0,5,5); button.Background=Ink(3,18,25);
   button.IsEnabledChanged += delegate { button.Opacity=button.IsEnabled?1:0.35; }; return button;
  }
  void CreateChatPanel(System.Windows.Controls.Canvas stage) {
   chatPreviewTimer=new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background) { Interval=TimeSpan.FromMilliseconds(50) };
   chatPreviewTimer.Tick+=delegate { chatPreviewTimer.Stop();if(!closing && chatPreviewText!=null) chatTranscript.Text=chatPreviewPrefix+chatPreviewText.ToString();chatPreviewText=null;chatPreviewPrefix=null; };
   var layout=new DockPanel();
   chatAccount=Label("CHATGPT / NOT CONNECTED",10,cyan); chatAccount.Margin=new Thickness(0,1,0,5); DockPanel.SetDock(chatAccount,Dock.Top); layout.Children.Add(chatAccount);
   var footer=new StackPanel();
   chatStatus=Label("Connect below to ask " + ChatDisplayName + " a question.",10,cyan); chatStatus.MinHeight=24; chatStatus.Margin=new Thickness(0,4,0,5); footer.Children.Add(chatStatus);
   chatInput=new TextBox { AcceptsReturn=true, TextWrapping=TextWrapping.Wrap, Height=52, VerticalScrollBarVisibility=ScrollBarVisibility.Auto, Background=Ink(3,19,27), Foreground=Brushes.White, CaretBrush=cyan, BorderBrush=Ink(0,103,115), Padding=new Thickness(7), FontSize=13, MaxLength=4000, Margin=new Thickness(0,0,0,6), ToolTip="Type a question. Enter sends; Shift+Enter adds a line." }; footer.Children.Add(chatInput);
   // Catch Enter before the multiline TextBox consumes it as a newline.
   chatInput.PreviewKeyDown += delegate(object sender,KeyEventArgs e) { if(e.Key==Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift)==0) { e.Handled=true; if(!e.IsRepeat) SendChat(); } };
   var actions=new WrapPanel(); chatSend=ChatControl("SEND",60,SendChat); actions.Children.Add(chatSend); chatVoice=ChatControl("VOICE / F9",105,BeginQuestionDictation); actions.Children.Add(chatVoice); chatCancel=ChatControl("CANCEL",65,CancelChatOperation); actions.Children.Add(chatCancel);chatNameButton=ChatControl("NAME",60,OpenChatNameSettings);chatNameButton.ToolTip="Choose a name for your chat";actions.Children.Add(chatNameButton); footer.Children.Add(actions);
   var speech=new WrapPanel(); readChatAloud=new CheckBox { Content="Read aloud", IsChecked=true, Foreground=cyan, FontSize=10, VerticalAlignment=VerticalAlignment.Center, Margin=new Thickness(0,0,10,5), Width=91 }; speech.Children.Add(readChatAloud); speech.Children.Add(ChatControl("STOP VOICE",86,StopCurrentSpeech)); chatClear=ChatControl("NEW CHAT",84,ClearChat); speech.Children.Add(chatClear); footer.Children.Add(speech);
   var settings=new StackPanel { Margin=new Thickness(0,6,0,2) };
   chatConnect=Action("Continue with ChatGPT",ConnectChat); chatConnect.Width=215; chatConnect.Padding=new Thickness(6,5,6,5); chatConnect.Margin=new Thickness(0,0,0,6); chatConnect.Background=Brushes.White; chatConnect.Foreground=Brushes.Black; chatConnect.FontFamily=new FontFamily("Segoe UI"); chatConnect.Style=new Style(typeof(Button)); settings.Children.Add(chatConnect);
   settings.Children.Add(Label("MODEL",9,muted));
   chatModels=new ComboBox { HorizontalAlignment=HorizontalAlignment.Stretch, Foreground=Brushes.Black, Padding=new Thickness(4), FontSize=11, Margin=new Thickness(0,0,0,6) }; settings.Children.Add(chatModels);
   chatModels.SelectionChanged += delegate { if(!refreshingModels && !chatBusy && chatClient!=null && chatModels.SelectedItem is ChatModel) chatClient.SetModel(((ChatModel)chatModels.SelectedItem).Slug); };
   var accountButtons=new WrapPanel(); chatRefresh=ChatControl("REFRESH",85,RefreshChatModels); accountButtons.Children.Add(chatRefresh); chatDisconnect=ChatControl("DISCONNECT",100,DisconnectChat); accountButtons.Children.Add(chatDisconnect); settings.Children.Add(accountButtons);
   settings.Children.Add(Label("Questions use your ChatGPT plan. Replies use your saved voice service.",9,muted));
   chatSettings=new Expander { Header="ACCOUNT / MODEL", Content=settings, Foreground=muted, FontSize=10, Margin=new Thickness(0,1,0,4) }; footer.Children.Add(chatSettings);
   var hint=Label("ENTER / SEND   F9 / ASK BY VOICE",9,muted); hint.Margin=new Thickness(0,2,0,2); footer.Children.Add(hint);
   DockPanel.SetDock(footer,Dock.Bottom); layout.Children.Add(footer);
   chatTranscript=new TextBox { IsReadOnly=true, TextWrapping=TextWrapping.Wrap, VerticalScrollBarVisibility=ScrollBarVisibility.Auto, Background=new SolidColorBrush(Color.FromArgb(155,0,10,17)), Foreground=Brushes.White, BorderThickness=new Thickness(0), Padding=new Thickness(7), FontFamily=new FontFamily("Segoe UI"), FontSize=13, Margin=new Thickness(0,3,0,3), ToolTip="Your conversation with " + ChatDisplayName + ". Questions and recent chat context are sent to OpenAI." }; layout.Children.Add(chatTranscript);
   chatTranscript.TextChanged+=delegate { ScrollChatToLatest(); };
   chatTranscript.Loaded+=delegate { ScrollChatToLatest(); };
   chatTranscript.SizeChanged+=delegate { ScrollChatToLatest(); };
   chatDrawer=new Border { Background=Brushes.Transparent, Child=ReferenceSection(ChatDisplayUpper + " / CONVERSATION",layout,328,436,out chatHeading) }; Place(stage,chatDrawer,816,210,328,436);
   KeyDown += delegate(object sender,KeyEventArgs e) { if(e.Key==Key.F9) { OpenChatPanel(); BeginQuestionDictation(); e.Handled=true; } };
  }
  public void OpenChatPanel() {
   appDrawer.Visibility=Visibility.Collapsed; sensorDrawer.Visibility=Visibility.Collapsed;
   if(chatClient!=null && chatClient.Connected && chatModels.Items.Count==0 && !chatBusy) RefreshChatModels();
   if(chatClient!=null && !chatClient.Connected) chatSettings.IsExpanded=true;
   chatInput.Focus();
   ScrollChatToLatest();
  }
  void HideChatPanel() { if(questionListening) EndQuestionDictation(); }
  void UpdateChatAccount() {
   RefreshReactorActivity();
   if(chatClient==null) return;
   chatAccount.Text=chatClient.Connected?"CHATGPT / CONNECTED":"CHATGPT / NOT CONNECTED"; chatAccount.ToolTip=chatClient.AccountLabel;
   chatConnect.IsEnabled=!chatBusy; chatDisconnect.IsEnabled=!chatBusy && chatClient.Connected; chatRefresh.IsEnabled=!chatBusy && chatClient.Connected;
   chatSend.IsEnabled=!chatBusy && chatClient.Connected && chatModels.SelectedItem is ChatModel; chatVoice.IsEnabled=chatSend.IsEnabled;
   chatModels.IsEnabled=!chatBusy; chatClear.IsEnabled=!chatBusy;
   chatCancel.IsEnabled=chatBusy || questionListening;
   chatNameButton.IsEnabled=!chatBusy && !questionListening;
  }
  void SetChatBusy(bool busy) { chatBusy=busy;if(!busy) chatQuestionInFlight=false; UpdateChatAccount(); }
  void ScrollChatToLatest() {
   if(closing || chatTranscript==null || Dispatcher.HasShutdownStarted || chatScrollPending) return;
   chatScrollPending=true;
   // Wait until WPF has measured wrapped text and the final transcript viewport.
   // Coalesce streaming chunks into one scroll without moving input focus.
   Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(delegate {
    chatScrollPending=false;
    if(closing || Dispatcher.HasShutdownStarted || !chatTranscript.IsLoaded) return;
    chatTranscript.ScrollToEnd();
   }));
  }
  void QueueChatPreview(string prefix,StringBuilder partial) { chatPreviewPrefix=prefix;chatPreviewText=partial;if(!chatPreviewTimer.IsEnabled) chatPreviewTimer.Start(); }
  void CancelChatPreview() { chatPreviewTimer.Stop();chatPreviewText=null;chatPreviewPrefix=null; }
  void PaintChatHistory() { chatTranscript.Text=String.Join("\n\n",chatMessages.Select(m => (m.Role=="user" ? "YOU" : ChatDisplayUpper) + "\n" + m.Content)); ScrollChatToLatest(); }
  void SaveChatHistory() { ChatGptClient.WriteProtected(ChatHistoryPath,chatMessages); }
  void ClearChat() { if(chatBusy) return; CancelChatPreview();StopCurrentSpeech(); chatMessages.Clear(); chatTranscript.Clear(); SaveChatHistory(); chatStatus.Text="New conversation. Your questions stay in this app's separate chat history."; }
  CancellationToken StartChatOperation(int seconds) { if(chatOperation!=null) chatOperation.Dispose(); chatOperation=CancellationTokenSource.CreateLinkedTokenSource(chatLifetime.Token); chatOperation.CancelAfter(TimeSpan.FromSeconds(seconds)); SetChatBusy(true); return chatOperation.Token; }
  void OnChatUI(Action action) { if(closing || Dispatcher.HasShutdownStarted) return; Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,new Action(delegate { if(!closing) { try { action(); } catch(Exception ex) { ReportChatFailure(ex); SetChatBusy(false); } } })); }
  void ReportChatFailure(Exception error) { chatStatus.Text=error is OperationCanceledException ? "Request cancelled or timed out. You can try again." : error is ChatConnectionException ? error.Message : "The ChatGPT connection could not be completed. Try again."; Log(chatStatus.Text); }
  public void ConnectChat() {
   if(chatBusy) return; if(questionListening) EndQuestionDictation(); StopCurrentSpeech();
   string priorAccount = chatClient.AccountLabel; var token=StartChatOperation(310); chatStatus.Text="Finish sign-in and approve " + AssistantIdentity.CurrentName + " in your browser. No API key is required.";
   Task.Run(delegate {
    try {
     chatClient.SignIn(delegate(string url) { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName=url, UseShellExecute=true }); },token);
     var models=chatClient.Models(token);
     OnChatUI(delegate { if(priorAccount!=chatClient.AccountLabel) { chatMessages.Clear(); SaveChatHistory(); PaintChatHistory(); } FillChatModels(models); chatStatus.Text=models.Count>0 ? "Connected. Type a question or use VOICE / F9." : "Signed in, but no models are available for this account. Check plan-sharing permissions."; chatSettings.IsExpanded=models.Count==0; SetChatBusy(false); });
    } catch(Exception ex) { OnChatUI(delegate { ReportChatFailure(ex); SetChatBusy(false); }); }
   });
  }
  void FillChatModels(List<ChatModel> models) {
   refreshingModels=true; chatModels.ItemsSource=models; chatModels.SelectedItem=models.FirstOrDefault(m=>m.Slug==chatClient.SelectedModel) ?? models.FirstOrDefault(); refreshingModels=false;
   if(chatModels.SelectedItem is ChatModel) chatClient.SetModel(((ChatModel)chatModels.SelectedItem).Slug);
  }
  void RefreshChatModels() {
   if(chatBusy || !chatClient.Connected) return; var token=StartChatOperation(35); chatStatus.Text="Checking models available to your ChatGPT account…";
   Task.Run(delegate { try { var models=chatClient.Models(token); OnChatUI(delegate { FillChatModels(models); chatStatus.Text=models.Count>0 ? "Ready. Choose a model and ask a question." : "No models were returned for this account. Check plan sharing in ChatGPT."; SetChatBusy(false); }); } catch(Exception ex) { OnChatUI(delegate { ReportChatFailure(ex); SetChatBusy(false); }); } });
  }
  void DisconnectChat() { if(chatBusy) return; if(questionListening) EndQuestionDictation(); StopCurrentSpeech(); chatClient.Disconnect(); chatModels.ItemsSource=null; chatMessages.Clear(); SaveChatHistory(); PaintChatHistory(); chatSettings.IsExpanded=true; chatStatus.Text="Disconnected. Connect below to ask a question."; UpdateChatAccount(); }
  void CancelChatOperation() { if(questionListening) { EndQuestionDictation(); chatStatus.Text="Question microphone closed."; } if(chatBusy && chatOperation!=null) { chatOperation.Cancel();chatQuestionInFlight=false;RefreshReactorActivity(); chatStatus.Text="Cancelling…"; } }
  void SendChat() {
   if(chatBusy) return;
   if(!chatClient.Connected || !(chatModels.SelectedItem is ChatModel)) { chatStatus.Text="Connect with ChatGPT and select an available model first."; return; }
   string question=chatInput.Text.Trim(); if(question.Length==0) return;
   if(questionListening) EndQuestionDictation(); StopCurrentSpeech();
   var history=chatMessages.Select(m=>new ChatMessage { Role=m.Role,Content=m.Content }).ToList();
   while(history.Count>=38 || history.Sum(m=>m.Content.Length)+question.Length>28000) { if(history.Count==0) break; history.RemoveAt(0); if(history.Count>0 && history[0].Role=="assistant") history.RemoveAt(0); }
   history.Add(new ChatMessage { Role="user",Content=question });
   string model=((ChatModel)chatModels.SelectedItem).Slug; var token=StartChatOperation(150); chatQuestionInFlight=true;RefreshReactorActivity();chatInput.Clear(); chatStatus.Text="Thinking…";
   string chatName=ChatDisplayName;
   string prefix=String.Join("\n\n",history.Select(m=>(m.Role=="user"?"YOU":ChatDisplayUpper)+"\n"+m.Content))+"\n\n" + ChatDisplayUpper + "\n"; chatTranscript.Text=prefix; var partial=new StringBuilder();
   Task.Run(delegate {
    try {
     string answer=chatClient.Ask(model,history,delegate(string value) { OnChatUI(delegate { partial.Append(value); QueueChatPreview(prefix,partial); }); },token,chatName);
     OnChatUI(delegate { CancelChatPreview();history.Add(new ChatMessage { Role="assistant",Content=answer }); while(history.Count>40 || history.Sum(m=>m.Content.Length)>32000) { history.RemoveAt(0); if(history.Count>0 && history[0].Role=="assistant") history.RemoveAt(0); } chatMessages.Clear(); chatMessages.AddRange(history); SaveChatHistory(); PaintChatHistory(); chatStatus.Text="Reply complete."; SetChatBusy(false); if(readChatAloud.IsChecked==true) SpeakChatReply(answer); });
    } catch(Exception ex) { OnChatUI(delegate { CancelChatPreview();chatTranscript.Text=prefix+partial+"\n\n[Reply incomplete — not saved to conversation]"; if(String.IsNullOrWhiteSpace(chatInput.Text)) chatInput.Text=question; ReportChatFailure(ex); SetChatBusy(false); }); }
   });
  }
  void SpeakChatReply(string answer) {
   if(thermalVoiceInProgress) { queuedChatSpeech=answer; return; }
   SpeakText(ChatSpeechText(answer),null,false,delegate(string error) { if(error!=null) chatStatus.Text=error; });
  }
  public static string ChatSpeechText(string text) {
   string result=Regex.Replace(text,@"```[\s\S]*?```"," Code is shown on screen. "); result=Regex.Replace(result,@"\[([^\]]+)\]\([^)]*\)","$1"); result=Regex.Replace(result,@"[*#`_]","");
   if(result.Length>1100) { result=result.Substring(0,1060); int boundary=result.LastIndexOf(' '); if(boundary>900) result=result.Substring(0,boundary); result+=". The rest is on screen."; }
   return result.Trim();
  }
  void BeginQuestionDictation() {
   if(chatBusy || speaking || questionListening) { chatStatus.Text="Wait for the current reply, or use STOP SPEAKING first."; return; }
   OpenChatPanel(); if(chatBusy || !chatClient.Connected || !(chatModels.SelectedItem is ChatModel)) { chatStatus.Text="Connect with ChatGPT and select a model before using voice questions."; return; }
   if(recognizer==null) { chatStatus.Text="Windows English dictation is unavailable. Type your question instead."; return; }
   resumeLaunchListening=continuous; continuous=false; pttUntil=DateTime.MinValue; questionListening=true; questionUntil=DateTime.UtcNow.AddSeconds(15); SetListenLabel("LISTEN");
   chatStatus.Text="Listening for one question. It will be sent to ChatGPT when recognized."; SetState("ASK " + ChatDisplayUpper,"Speak your question. Up to 15 seconds."); UpdateChatAccount(); UpdateGrammar();
  }
  void EndQuestionDictation() {
   questionListening=false; questionUntil=DateTime.MinValue; continuous=resumeLaunchListening; resumeLaunchListening=false; SetListenLabel(continuous?"STOP LISTENING":"LISTEN"); UpdateGrammar(); UpdateChatAccount(); SetState(continuous?"LISTENING":"STANDBY","Question microphone closed.");
  }
  void OnQuestionRecognized(string text, float confidence) {
   if(screenPreferences.Enabled && !WindowsScreenPower.Unlocked) { if(questionListening) EndQuestionDictation();return; }
   if(!questionListening || speaking || closing || DateTime.UtcNow<suppressUntil) return;
   if(confidence<0.55f || String.IsNullOrWhiteSpace(text)) { chatStatus.Text="I didn't catch that clearly. Try speaking again."; return; }
   TouchScreenActivity(); EndQuestionDictation(); chatInput.Text=text; SendChat();
  }
  void CheckQuestionTimeout() { if(questionListening && DateTime.UtcNow>questionUntil) { EndQuestionDictation(); chatStatus.Text="Question listening timed out. Choose ASK BY VOICE to try again, or type your question."; } }
  void CloseChat() { CancelChatPreview();chatLifetime.Cancel(); if(chatOperation!=null) chatOperation.Cancel();chatQuestionInFlight=false;questionListening=false;RefreshReactorActivity();foreach(var rotor in reactorRotors) rotor.Rotation.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty,null); }
 }
}
