using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using ZmanLenagen.Core;
namespace ZmanLenagen.App;
sealed class PracticeTools : Grid, IDisposable {
 readonly Viewbox circleView=new(){Stretch=Stretch.Uniform,Height=300};
 readonly Store store;readonly Func<PracticeStep?> context;readonly Action openArchive;
 readonly RecordingArchive archive;readonly RecorderAudio recorder;readonly MetronomeAudio metronome=new();
 readonly DispatcherTimer pulse=new(){Interval=TimeSpan.FromMilliseconds(30)};
 readonly DispatcherTimer retention=new(){Interval=TimeSpan.FromHours(1)};
 readonly Stopwatch visualClock=new();readonly Stopwatch tapClock=Stopwatch.StartNew();readonly TapTempo tap=new();
 readonly TextBox bpm=new(){Text="80",Width=60,FlowDirection=FlowDirection.LeftToRight};
 readonly Slider tempo=new(){Minimum=20,Maximum=300,Value=80,IsSnapToTickEnabled=true,TickFrequency=1,Margin=new Thickness(5)};
 readonly ComboBox signature=new(),subdivision=new(),sound=new();
 readonly CheckBox accent=new(){Content="דגש בפעימה הראשונה",IsChecked=true,Margin=new Thickness(5,8,5,8)};
 readonly StackPanel lights=new(){Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,FlowDirection=FlowDirection.LeftToRight};
 readonly Button metroButton,recordButton,playButton;
 readonly TextBlock recordStatus=Label("לחיצה אחת, והנגינה נשמרת."),scaleStatus=Label("בחר סולם, או תן למזל לבחור.");
 Recording? latest;DateTimeOffset recordStart;bool changing,disposed;int visualBeat=-1,appliedBpm=80;
 static Brush Color(string value)=>(Brush)new BrushConverter().ConvertFromString(value)!;
 static TextBlock Label(string value,int size=14)=>new(){Text=value,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(5),TextAlignment=TextAlignment.Center};
 static Button Btn(string value,Action action){var b=new Button{Content=value,Padding=new Thickness(10,7,10,7),Margin=new Thickness(3)};b.Click+=(s,e)=>{try{action();}catch(Exception ex){MessageBox.Show(ex.Message,"לא הצלחנו להפעיל את העזר");}};return b;}
 public PracticeTools(Store store,Func<PracticeStep?> context,Action openArchive) {
  this.store=store;this.context=context;this.openArchive=openArchive;
  archive=new RecordingArchive(System.IO.Path.Combine(Store.Folder,"recordings"));recorder=new RecorderAudio(archive);latest=archive.List().FirstOrDefault();
  FlowDirection=FlowDirection.LeftToRight;Height=390;Margin=new Thickness(14,4,14,12);
  ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});ColumnDefinitions.Add(new(){Width=new GridLength(1.6,GridUnitType.Star)});ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
  var left=Card(0);left.Children.Add(Label("מטרונום",20));
  var tempoLine=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,FlowDirection=FlowDirection.LeftToRight};
  tempoLine.Children.Add(Btn("−",()=>SetBpm(CurrentBpm()-1)));tempoLine.Children.Add(bpm);tempoLine.Children.Add(Label("BPM"));tempoLine.Children.Add(Btn("+",()=>SetBpm(CurrentBpm()+1)));left.Children.Add(tempoLine);left.Children.Add(tempo);
  System.Windows.Automation.AutomationProperties.SetName(bpm,"קצב פעימות לדקה, בין 20 ל־300");System.Windows.Automation.AutomationProperties.SetName(tempo,"קצב מטרונום");
  bpm.LostFocus+=(s,e)=>{try{SetBpm(CurrentBpm());}catch{bpm.Text=((int)tempo.Value).ToString();MessageBox.Show("בחר קצב שלם בין 20 ל־300 BPM.");}};
  bpm.KeyDown+=(s,e)=>{if(e.Key==System.Windows.Input.Key.Enter){try{SetBpm(CurrentBpm());}catch{MessageBox.Show("בחר קצב שלם בין 20 ל־300 BPM.");}}};
  tempo.ValueChanged+=(s,e)=>{if(!changing)SetBpm((int)tempo.Value);};
  var choices=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center};left.Children.Add(choices);
  Combo(signature,new[]{"4/4","3/4","6/8","2/4","5/4","7/8"},"משקל",choices);
  Combo(subdivision,new[]{"ללא חלוקה","חלוקה ל־2","שלישיות","חלוקה ל־4"},"חלוקת פעימה",choices);
  Combo(sound,new[]{"קליק","עץ","פעמון"},"צליל מטרונום",choices);
  left.Children.Add(accent);left.Children.Add(lights);
  var metroActions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center};left.Children.Add(metroActions);
  metroButton=Btn("הפעל",ToggleMetronome);metroActions.Children.Add(metroButton);metroActions.Children.Add(Btn("Tap Tempo",()=>{var v=tap.Tap(tapClock.Elapsed.TotalSeconds);if(v.HasValue)SetBpm(v.Value);}));
  left.Children.Add(Label("ב־6/8 וב־7/8 כל שמינית היא פעימה.",11));
  signature.SelectionChanged+=(s,e)=>Changed();subdivision.SelectionChanged+=(s,e)=>Changed();sound.SelectionChanged+=(s,e)=>Changed();accent.Checked+=(s,e)=>Changed();accent.Unchecked+=(s,e)=>Changed();BuildLights();
  var middle=Card(1);middle.Children.Add(Label("מעגל הקווינטות",20));
  var canvas=new Canvas{Width=460,Height=460,FlowDirection=FlowDirection.LeftToRight};
  var ring=new Ellipse{Width=342,Height=342,Stroke=Color("#E2DDF7"),StrokeThickness=52};Canvas.SetLeft(ring,59);Canvas.SetTop(ring,59);canvas.Children.Add(ring);
  var inner=new Ellipse{Width=224,Height=224,Stroke=Color("#E8F5F0"),StrokeThickness=37};Canvas.SetLeft(inner,118);Canvas.SetTop(inner,118);canvas.Children.Add(inner);
  var selections=new List<Button>();
  void Select(Button b,string name,bool minor){foreach(var item in selections)item.Background=Brushes.Transparent;b.Background=Color(minor?"#A7E7CD":"#C8B9F7");scaleStatus.Text=name+(minor?" · מינור":" · מז׳ור");scaleStatus.FlowDirection=FlowDirection.LeftToRight;}
  for(int i=0;i<12;i++) {
   var key=Fifths.Keys[i];var angle=(i*30-90)*Math.PI/180;
   void Note(string name,double radius,bool minor) {
    Button? b=null;b=Btn(name,()=>Select(b!,name,minor));b.Width=i==6?142:name.Length*(minor?13:20)+16;b.Height=minor?26:40;b.FontSize=minor?19:25;b.FontWeight=FontWeights.SemiBold;b.Background=Brushes.Transparent;b.FlowDirection=FlowDirection.LeftToRight;b.Padding=new Thickness(0);b.Margin=new Thickness(0);b.ToolTip=name+(minor?" — מינור":" — מז׳ור");
    Canvas.SetLeft(b,230+radius*Math.Cos(angle)-b.Width/2);Canvas.SetTop(b,230+(minor&&i==6?130:radius)*Math.Sin(angle)-b.Height/2);canvas.Children.Add(b);selections.Add(b);
   }
   Note(key.Major,166,false);Note(key.Minor,111,true);
  }
  var random=Btn("סולם\nאקראי",()=>{var n=Random.Shared.Next(24);var b=selections[n];Select(b,n%2==0?Fifths.Keys[n/2].Major:Fifths.Keys[n/2].Minor,n%2!=0);});random.Width=84;random.Height=62;random.FontSize=15;random.Padding=new Thickness(4);Canvas.SetLeft(random,188);Canvas.SetTop(random,199);canvas.Children.Add(random);
  circleView.Child=canvas;middle.Children.Add(circleView);middle.Children.Add(scaleStatus);
  var right=Card(2);right.Children.Add(Label("מקליט קול",20));right.Children.Add(Label("תפוס את הרגע. אפשר להקשיב מיד."));
  recordButton=Btn("● הקלט",ToggleRecording);recordButton.Background=Color("#FFD8E0");right.Children.Add(recordButton);
  playButton=Btn("▶ השמע הקלטה אחרונה",()=>{if(recorder.PlayingId!=null)recorder.StopPlayback();else if(latest!=null)recorder.Play(latest);UpdateRecording();});right.Children.Add(playButton);right.Children.Add(recordStatus);
  right.Children.Add(Btn("כל ההקלטות",openArchive));right.Children.Add(Label("נשמר בחשבון שלך · נמחק אחרי 7 ימים\nאפשר לייצא מתוך כל ההקלטות.",12));
  recorder.Completed+=(r,error)=>Dispatcher.BeginInvoke(()=>{if(disposed)return;if(r!=null){archive.Enrich(r,store.Steps());latest=r;}recordStatus.Text=error==null?"נשמר! אפשר להקשיב עכשיו.":"ההקלטה נעצרה: "+error.Message;UpdateRecording();});
  pulse.Tick+=(s,e)=>{if(metronome.Playing){var beat=(int)(Math.Max(0,visualClock.Elapsed.TotalSeconds-.04)*appliedBpm/60)%Beats();if(beat!=visualBeat){visualBeat=beat;for(int i=0;i<lights.Children.Count;i++)((Ellipse)lights.Children[i]).Fill=Color(i==beat?(i==0&&accent.IsChecked==true?"#E27398":"#8266D2"):"#E6E2F3");}}if(recorder.Recording)recordStatus.Text="מקליטים · "+(DateTimeOffset.UtcNow-recordStart).ToString(@"mm\:ss");UpdateRecording();};pulse.Start();
  retention.Tick+=(s,e)=>{if(archive.List().Any(r=>r.Id==recorder.PlayingId&&r.Started<=DateTimeOffset.UtcNow.AddDays(-7)))recorder.StopPlayback();archive.Cleanup(recorder.CurrentId);latest=archive.List().FirstOrDefault();UpdateRecording();};retention.Start();UpdateRecording();
 }
 StackPanel Card(int column) {
  var panel=new StackPanel{FlowDirection=FlowDirection.RightToLeft};var card=new Border{Background=Brushes.White,CornerRadius=new CornerRadius(18),Padding=new Thickness(10),Margin=new Thickness(4),Child=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}};Grid.SetColumn(card,column);Children.Add(card);return panel;
 }
 static void Combo(ComboBox combo,string[] items,string name,Panel parent){foreach(var i in items)combo.Items.Add(i);combo.SelectedIndex=0;combo.Margin=new Thickness(3);combo.Padding=new Thickness(4);combo.FlowDirection=FlowDirection.LeftToRight;System.Windows.Automation.AutomationProperties.SetName(combo,name);parent.Children.Add(combo);}
 int CurrentBpm(){if(!int.TryParse(bpm.Text,out var n)||n<20||n>300)throw new ArgumentException("בחר BPM שלם בין 20 ל־300.");return n;}
 int Beats()=>int.Parse(((string)signature.SelectedItem).Split('/')[0]);
 void SetBpm(int value){value=Math.Clamp(value,20,300);changing=true;bpm.Text=value.ToString();tempo.Value=value;changing=false;Changed();}
 void Changed(){if(metroButton==null)return;BuildLights();if(metronome.Playing){try{StartMetronome();}catch(Exception ex){StopMetronome();MessageBox.Show(ex.Message,"לא הצלחנו להפעיל מטרונום");}}}
 void BuildLights(){lights.Children.Clear();for(int i=0;i<Beats();i++)lights.Children.Add(new Ellipse{Width=14,Height=14,Margin=new Thickness(5,4,5,4),Fill=Color("#E6E2F3")});visualBeat=-1;}
 void StartMetronome(){appliedBpm=CurrentBpm();metronome.Start(appliedBpm,Beats(),subdivision.SelectedIndex+1,sound.SelectedIndex,accent.IsChecked==true);visualClock.Restart();visualBeat=-1;metroButton.Content="עצור";}
 void StopMetronome(){metronome.Stop();visualClock.Stop();metroButton.Content="הפעל";BuildLights();}
 void ToggleMetronome(){if(metronome.Playing)StopMetronome();else StartMetronome();}
 void ToggleRecording(){if(recorder.Recording)recorder.Stop();else{try{recorder.Start(context());}catch(Exception ex){throw new InvalidOperationException("לא הצלחנו להתחיל הקלטה. בדוק שמחובר מיקרופון ושבהגדרות הפרטיות של Windows מופעלת גישת מיקרופון ליישומי שולחן עבודה.\n"+ex.Message);}recordStart=DateTimeOffset.UtcNow;}UpdateRecording();}
 void UpdateRecording(){recordButton.Content=recorder.Stopping?"שומרים…":recorder.Recording?"■ עצור ושמור":"● הקלט";recordButton.IsEnabled=!recorder.Stopping;playButton.IsEnabled=latest!=null&&!recorder.Recording;playButton.Content=recorder.PlayingId==null?"▶ השמע הקלטה אחרונה":"■ עצור השמעה";}
 public void FeedbackSaved(PracticeStep step){foreach(var r in archive.List().Where(r=>r.StepId==step.Id))archive.Enrich(r,[step]);}
 public void ShowArchive(StackPanel page) {
  recorder.StopPlayback();archive.Cleanup(recorder.CurrentId);var all=archive.List();var steps=store.Steps();foreach(var r in all)archive.Enrich(r,steps);
  page.Children.Add(Label("ההקלטות נשמרות לשבעה ימים. המחיקה מתבצעת כשהתוכנה פתוחה או בפתיחה הבאה. ייצוא ZIP כולל קובצי WAV ותגיות. ההקלטה משויכת לשלב שבו התחילה.",15));
  var selected=new HashSet<string>();var buttons=new WrapPanel();page.Children.Add(buttons);
  async void Export(bool onlySelected) {
   var items=all.Where(r=>!onlySelected||selected.Contains(r.Id)).ToList();if(items.Count==0){MessageBox.Show("בחר לפחות הקלטה אחת.");return;}
   var dialog=new SaveFileDialog{FileName="zman-recordings-"+DateTime.Now.ToString("yyyy-MM-dd"),Filter="הקלטות ותגיות ZIP|*.zip"};if(dialog.ShowDialog()!=true)return;
   buttons.IsEnabled=false;retention.Stop();
   try{await System.Threading.Tasks.Task.Run(()=>archive.Export(dialog.FileName,items));MessageBox.Show("ההקלטות נשמרו. אפשר לקחת אותן איתך.");}catch(Exception ex){MessageBox.Show(ex.Message,"הייצוא לא הושלם");}finally{buttons.IsEnabled=true;if(!disposed)retention.Start();}
  }
  buttons.Children.Add(Btn("ייצא נבחרות",()=>Export(true)));buttons.Children.Add(Btn("ייצא את כולן",()=>Export(false)));
  if(all.Count==0)page.Children.Add(Label("עוד אין הקלטות. המקליט מחכה לך בתחתית המסך.",20));
  foreach(var r in all) {
   var row=new StackPanel{Margin=new Thickness(8)};var pick=new CheckBox{Content=r.Tag+" · "+r.Started.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),Margin=new Thickness(5),FontSize=18};pick.Checked+=(s,e)=>selected.Add(r.Id);pick.Unchecked+=(s,e)=>selected.Remove(r.Id);row.Children.Add(pick);
   row.Children.Add(Label($"משך: {(r.Ended-r.Started)?.TotalSeconds:0} שניות · משוב: {Feedback(r.Feedback)}\n{r.Notes}"+(r.Recovered?"\nשוחזרה לאחר סגירה בלתי צפויה":"")));
   var actions=new WrapPanel();actions.Children.Add(Btn("▶ השמע",()=>{if(recorder.Recording)throw new InvalidOperationException("עצור ושמור את ההקלטה הנוכחית לפני ההשמעה.");recorder.Play(r);}));actions.Children.Add(Btn("■ עצור",recorder.StopPlayback));row.Children.Add(actions);
   page.Children.Add(new Border{Child=row,CornerRadius=new CornerRadius(14),Background=Brushes.White,Margin=new Thickness(5)});
  }
 }
 static string Feedback(string? value)=>value switch{"yes"=>"כן","half"=>"חצי־חצי","no"=>"לא",_=>"טרם נוסף"};
 public void Resize(double windowHeight){Height=Math.Clamp(windowHeight*.44,210,390);circleView.Height=Math.Max(130,Height-90);}
 public void Suspend(){StopMetronome();recorder.Stop();recorder.StopPlayback();}
 public void Dispose(){if(disposed)return;disposed=true;pulse.Stop();retention.Stop();metronome.Dispose();recorder.Dispose();}
}
