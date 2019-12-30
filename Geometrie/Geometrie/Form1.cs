using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using IniParser;
using IniParser.Model;
using System.Threading;
using System.Data.SqlClient;
using System.IO;

namespace Geometrie
{
    public partial class Form1 : Form
    {
        // Global
        string Kusovnik;
        string Serial;
        string Worker;

        string Kusovnik_tmp;
        string Serial_tmp;
        string Worker_tmp;

        Boolean KusovnikDone;
        Boolean SerialDone;
        Boolean WorkerDone;

        Boolean GetAmp;
        Boolean GetLeftMax;
        Boolean GetRightMax;
        
        // Save and DB values
        Double ZeroValue;
        Double ZeroValue_Min;
        Double ZeroValue_Max;
               
        Double LeftMax_Min;
        Double LeftMax_Max;
        Double LeftMax;
        
        Double RightMax_Min;
        Double RightMax_Max;
        Double RightMax;

        Double ConvergenceMin;
        Double ConvergenceMax;
        Double Convergence;

        // Can I continue?
        Boolean ConvergenceSave;
        Boolean LeftMaxSave;
        Boolean RightMaxSave;

        // Watch
        int Watch_DB = 0;
        int Watch_tag = 0;
        int Watch2_tag = 0;

        Boolean Final = true;

        //string TestAmp = "346545656\n34564656\n3545646\n345656\n356456546\n34564656\n35456\n346556\n3456456\n356\n";

        string Error = "";
        string FolderPath;

        string SystemPicsDir = "SystemPics";
        string SystemPic_KusovnikNOK = "Kusovnik_NOK.jpg";
        string SystemPic_FinalOK = "Vysledek_OK.jpg";
        string SystemPic_FinalNOK = "Vysledek_NOK.jpg";

        // COMs
        SerialPort Scanner;
        SerialPort Watch;
        SerialPort Watch2;
        SerialPort RedPedal;
        SerialPort YellowPedal;
        SerialPort Amp;
        SerialPort Quido;
        // INI
        FileIniDataParser parser;
        IniData settings;
        // Program´s steps
        public enum Step
        {
            Scan = 0,
            Amper = 1,
            Convergence = 2,
            LeftMax = 3,
            RightMax = 4,
            Save = 5
        }
        //Actual step
        Step step = 0;

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                CheckDB(); 
                SetListBox();
                ShowResults();
                lbl_Step.Text = "Skenování";
                             
                ConnectINI();           
                LoadFolderPath();
                CreateSystemPics();
                CreateConnections();

            }
            catch (Exception ex)
            {
                // Full error
                MessageBox.Show(this.Error + "\r" + ex.Message, "Chyba");
            }
        }

        public void CreateSystemPics()
        {
            string DirPath = Path.Combine(this.FolderPath, this.SystemPicsDir);
            if (!Directory.Exists(DirPath))
            {
                Directory.CreateDirectory(DirPath);
                MessageBox.Show("Vložte do " + DirPath + " tyto obrázky: \r" + "Špatně načtený kusovník: " + SystemPic_KusovnikNOK + "\r" + "Celkový výsledek OK: " + SystemPic_FinalOK + "\r" +"Celkový výsledek NOK: " + SystemPic_FinalNOK);
            }
        }

        public void ShowSystemImage(string img)
        {
            try
            {
                string path = Path.Combine(this.FolderPath, this.SystemPicsDir, img);
                pictureBox1.Image = new Bitmap(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // Database save
        public void SaveAll(Boolean reset = false)
        {
            try
            { 
                int ID_Material = 0;
                SqlDataReader dr = null;
                DBH mat = new DBH("select ID from Material where Kusovnik = @kusovnik");
                mat.AddParameterTyped("kusovnik", this.Kusovnik, SqlDbType.VarChar);
                mat.Open();
                dr = mat.ExecReader();
                if (dr.Read())
                {
                    ID_Material = dr.GetInt32(dr.GetOrdinal("ID"));
                }
                mat.Close();


                StringBuilder insert = new StringBuilder();

                insert.AppendLine("insert into Geometrie(seriove_cislo,pracovnik,id_material,sbihavost,vysledek,datumCas");
                if (this.GetAmp) { insert.Append(",nulovy_uhel_elektronicky"); }
                if (this.GetLeftMax) { insert.Append(",maximalni_levy_elektronicky"); }
                if (this.GetRightMax) { insert.Append(",maximalni_pravy_elektronicky"); }
                insert.Append(")");

                insert.AppendLine("values(@seriove_cislo,@pracovnik,@id_material,@sbihavost,@vysledek,@datumCas");
                if (this.GetAmp) { insert.Append(",@nulovy_uhel_elektronicky"); }
                if (this.GetLeftMax) { insert.Append(",@maximalni_levy_elektronicky"); }
                if (this.GetRightMax) { insert.Append(",@maximalni_pravy_elektronicky"); }
                insert.Append(")");

                DBH db = new DBH(insert.ToString());
                db.AddParameterTyped("seriove_cislo", this.Serial, SqlDbType.VarChar);
                db.AddParameterTyped("pracovnik", this.Worker, SqlDbType.VarChar);
                db.AddParameterTyped("id_material", ID_Material, SqlDbType.Int);

                if (!reset || this.step > Step.Convergence)
                { 
                    db.AddParameterTyped("sbihavost", this.Convergence, SqlDbType.Float);
                } else 
                {
                    db.AddParameterTyped("sbihavost", DBNull.Value, SqlDbType.Float);
                }

                db.AddParameterTyped("vysledek", this.Final, SqlDbType.Bit);
                db.AddParameterTyped("datumCas", DateTime.Now, SqlDbType.DateTime);

                if (this.GetAmp)
                {
                    if (!reset || this.step > Step.Amper) { 
                        db.AddParameterTyped("nulovy_uhel_elektronicky", this.ZeroValue, SqlDbType.Float);
                    } else
                    {
                        db.AddParameterTyped("nulovy_uhel_elektronicky", DBNull.Value, SqlDbType.Float);
                    }
                }

                if (this.GetLeftMax)
                {
                    if (!reset || this.step > Step.LeftMax)
                    {
                        db.AddParameterTyped("maximalni_levy_elektronicky", this.LeftMax, SqlDbType.Float);
                    } else
                    {
                        db.AddParameterTyped("maximalni_levy_elektronicky", DBNull.Value, SqlDbType.Float);
                    }
                }

                if (this.GetRightMax)
                {
                    if (!reset && this.step > Step.LeftMax)
                    { 
                        db.AddParameterTyped("maximalni_pravy_elektronicky", this.RightMax, SqlDbType.Float);
                    } else
                    {
                        db.AddParameterTyped("maximalni_pravy_elektronicky", DBNull.Value, SqlDbType.Float);
                    }
                }

                db.Open();
                db.Exec();
                db.Close();

                Boolean loc_Final = this.Final;

                Reset();

                if (loc_Final)
                {
                    ShowSystemImage(this.SystemPic_FinalOK);
                } else
                {
                    ShowSystemImage(this.SystemPic_FinalNOK);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #region History

        public void SetListBox()
        {
            listBox1.DrawItem += ListBox1_DrawItem;
            listBox1.DrawMode = DrawMode.OwnerDrawFixed;
        }

        public class MyListBoxItem
        {
            public MyListBoxItem(Color c, string m)
            {
                ItemColor = c;
                Message = m;
            }
            public Color ItemColor { get; set; }
            public string Message { get; set; }
        }

        private void ListBox1_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index >= 0)
            {
                MyListBoxItem item = listBox1.Items[e.Index] as MyListBoxItem; // Get the current item and cast it to MyListBoxItem
                if (item != null)
                {
                    e.Graphics.DrawString( // Draw the appropriate text in the ListBox
                        item.Message, // The message linked to the item
                          e.Font,    //listBox1.Font, // Take the font from the listbox
                        new SolidBrush(item.ItemColor), // Set the color 
                        0, // X pixel coordinate
                        e.Index * listBox1.ItemHeight // Y pixel coordinate.  Multiply the index by the ItemHeight defined in the listbox.
                    );
                }
                else
                {
                    // The item isn't a MyListBoxItem, do something about it
                }
            }
        }

        public void ShowResults()
        {
            listBox1.Items.Clear();

            string serial = "";
            Boolean result = false;

            SqlDataReader dr = null;
            DBH db = new DBH("select top (25) seriove_cislo, vysledek from Geometrie order by datumCas desc");
            db.Open();
            dr = db.ExecReader();
            while (dr.Read())
            {
                serial = dr.GetString(dr.GetOrdinal("seriove_cislo"));
                result = dr.GetBoolean(dr.GetOrdinal("vysledek"));

                if (result)
                {
                    // Green
                    listBox1.Items.Add(new MyListBoxItem(Color.Green, serial));
                }
                else
                {
                    // Red
                    listBox1.Items.Add(new MyListBoxItem(Color.Red, serial));
                }

            }
            db.Close();
        }

        #endregion

        #region Connections

        // Test DB
        public void CheckDB()
        {
            try
            {
                DBH db = new DBH("select 1");
                db.Open();
                db.Close();
            }catch (Exception ex)
            {
                MessageBox.Show("Nelze připojit do databáze", "Chyba databáze", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }

        }

        // Connects INI
        private void ConnectINI()
        {
            try
            {

                this.parser = new FileIniDataParser();
                this.settings = parser.ReadFile("settings.ini");

            } catch (Exception ex)
            {
                AddError("Chyba načtení ini");
                throw;
            }
        }

        // Gets path to the pictures
        public void LoadFolderPath()
        {
            try
            {
                this.FolderPath = this.settings["KusovnikSlozka"]["cesta"];
            } catch (Exception ex)
            {
                AddError("Chyba načtení cesty do složky s obrázky.");
                throw;
            }
        }

        // Connect all COMs
        private void CreateConnections()
        {
            try
            {
                // Read port names from ini
                string ScannerCOM = this.settings["Skener"]["port"];
                string WatchCOM = this.settings["Hodinky"]["port"];
                string WatchCOM2 = this.settings["Hodinky2"]["port"];
                //string RedPedalCOM = this.settings["CervenyPedal"]["port"];
                //string YellowPedalCOM = this.settings["ZlutyPedal"]["port"];
                string AmpCOM = this.settings["Ampermetr"]["port"];
                string TlacitkaCOM = this.settings["Tlacitka"]["port"];

                string ID_Watch = this.settings["Hodinky"]["ID"];
                string ID_Watch2 = this.settings["Hodinky2"]["ID"];

                lbl_ScannerCOM.Text = ScannerCOM;
                lbl_WatchCOM.Text = WatchCOM;
                lbl_WatchCOM2.Text = WatchCOM2;
                lbl_RedPedalCOM.Text = TlacitkaCOM;
                lbl_YellowPedalCOM.Text = TlacitkaCOM;

                this.Watch_tag = Int32.Parse(ID_Watch);
                this.Watch2_tag = Int32.Parse(ID_Watch2);

                // Create connections
                this.Scanner = new SerialPort(ScannerCOM, 115200, Parity.None, 8, StopBits.One);
                this.Watch = new SerialPort(WatchCOM, 9600, Parity.None, 8, StopBits.One);
                this.Watch2 = new SerialPort(WatchCOM2, 9600, Parity.None, 8, StopBits.One);
                //this.RedPedal = new SerialPort(RedPedalCOM, 4800, Parity.Even, 7, StopBits.Two);
                //this.YellowPedal = new SerialPort(YellowPedalCOM, 4800, Parity.Even, 7, StopBits.Two);
                this.Amp = new SerialPort(AmpCOM, 9600, Parity.None, 8, StopBits.One);
                this.Quido = new SerialPort(TlacitkaCOM, 9600, Parity.None, 8, StopBits.One);

                // Create new events for read execution
                Scanner.DataReceived += Scanner_DataReceived;
                Watch.DataReceived += Watch_DataReceived;
                Watch2.DataReceived += Watch2_DataReceived;
                //RedPedal.DataReceived += RedPedal_DataReceived;
                //YellowPedal.DataReceived += YellowPedal_DataReceived;
                Quido.DataReceived += Quido_DataReceived;


                // Open connections
                this.Scanner.Open();
                this.Watch.Open();
                this.Watch2.Open();
                //this.RedPedal.Open();
                //this.YellowPedal.Open();
                this.Amp.Open();
                this.Quido.Open();
            }
            catch (Exception ex)
            {
                AddError("Chyba připojení COM.");
                if (this.Scanner == null) { AddError("Skener není připojen."); }
                if (this.Watch == null) { AddError("Hodinky nejsou připojeny."); }
                if (this.Watch2 == null) { AddError("Hodinky 2 nejsou připojeny."); }
                if (this.Quido == null) { AddError("Tlačítka nejsou připojeny."); }
                //if (this.RedPedal == null) { AddError("Červený pedál není připojen."); }
                //if (this.YellowPedal == null) { AddError("Žlutý pedál není připojen."); }
                throw;
            }
        }

        #endregion

        #region Data recieved

        private void Watch_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            Thread.Sleep(100);
            if (this.step == Step.Convergence)
            {
                SetScan dlg_Sbihavost = new SetScan(SetConvergence);
                string input = this.Watch.ReadExisting().Replace("\r", "");

                if (input.StartsWith("+") | input.StartsWith("-")) {
                    this.Invoke(dlg_Sbihavost, input);
                }
            }
        }

        private void Watch2_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            Thread.Sleep(100);
            if (this.step == Step.Convergence)
            {
                SetScan dlg_Sbihavost = new SetScan(SetConvergence);
                string input = this.Watch2.ReadExisting().Replace("\r", "");

                if (input.StartsWith("+") | input.StartsWith("-"))
                {
                    this.Invoke(dlg_Sbihavost, input);
                }
            }
        }

        private void Scanner_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SetScan dlg_Kusovnik = new SetScan(SetKusovnik);
            SetScan dlg_Serial = new SetScan(SetSerial);
            SetScan dlg_Worker = new SetScan(SetWorker);

            string input = this.Scanner.ReadExisting().Replace("\r", "");
            int n = input.Count();

            switch (n)
            {
                case 10:
                    this.Invoke(dlg_Kusovnik, input);
                    break;
                case 9:
                    this.Invoke(dlg_Serial, input);
                    break;
                case 8:
                    this.Invoke(dlg_Worker, input);
                    break;
                default:
                    break;
            }
        }

        private void YellowPedal_DataReceived()//(object sender, SerialDataReceivedEventArgs e)
        {
            //this.Watch.Write("?\r"); // Give me actual value
            //YellowPedal.ReadExisting();
            Quido.ReadExisting();
            Work dlg_Yellow = new Work(ExecStage);
            this.Invoke(dlg_Yellow);
            Thread.Sleep(200);
        }

        private void RedPedal_DataReceived()//(object sender, SerialDataReceivedEventArgs e)
        {
            //Thread.Sleep(100);
            //this.Watch.Write("SET\r"); // Set 0
            //RedPedal.ReadExisting();
            Quido.ReadExisting();
            if (this.step == Step.Convergence)
            {
                if (this.Watch_DB == this.Watch_tag)
                {
                    this.Watch.Write("?\r");
                }

                if (this.Watch_DB == this.Watch2_tag)
                {
                    this.Watch2.Write("?\r");
                }
                
            } else
            { 
                Work dlg_Red = new Work(ConfirmStage);
                this.Invoke(dlg_Red);
            }

            Thread.Sleep(200);
        }

        private void Quido_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            //Read status
            int bytes = Quido.BytesToRead;
            byte[] buffer = new byte[bytes];
            Quido.Read(buffer, 0, bytes);
            Quido.DiscardInBuffer();

            int num = buffer[7];
            string status = Convert.ToString(num, 2).PadLeft(3, '0');

            // Input 1
            if (status[2] == '1' & status[1] == '0')
            {
                YellowPedal_DataReceived();
            }

            // Input 2
            if (status[1] == '1' & status[2] == '0')
            {
                RedPedal_DataReceived();
            }
        }

        #endregion

        #region Scanner

        delegate void SetScan(string msg);

        private void SetKusovnik(string kusovnik)
        {
            this.Kusovnik = kusovnik;
            //txt_Kusovnik.Text = kusovnik;
            SetScanDone(1, kusovnik);
        }

        private void SetSerial(string serial)
        {
            this.Serial = serial;
            //txt_SerioveCislo.Text = serial;
            SetScanDone(2, serial);
        }

        private void SetWorker(string worker)
        {
            this.Worker = worker;
            SetScanDone(3, worker);
        }

        public void SetScanDone(int i, string msg)
        {
            if ((this.KusovnikDone) && (this.SerialDone) && (this.WorkerDone))
            {
                this.KusovnikDone = false;
                this.SerialDone = false;
                this.WorkerDone = false;
                
                Reset();
            }

            if (i == 1)
            {
                this.Kusovnik_tmp = msg;
                this.KusovnikDone = true;
            }

            if (i == 2)
            {
                this.Serial_tmp = msg;
                this.SerialDone = true;
            }

            if (i == 3)
            {
                this.Worker_tmp = msg;
                this.WorkerDone = true;
            }

            if ((this.KusovnikDone) && (this.SerialDone) && (this.WorkerDone))
            {
                resetToolStripMenuItem.Enabled = true;
                this.Kusovnik = this.Kusovnik_tmp;
                this.Serial = this.Serial_tmp;
                this.Worker = this.Worker_tmp;

                txt_Kusovnik.Text = this.Kusovnik;
                txt_SerioveCislo.Text = this.Serial;

                SqlDataReader dr = null;
                DBH db = new DBH("select * from Material where kusovnik = @kusovnik and mereni_geometrie is not null");
                db.AddParameterTyped("kusovnik", this.Kusovnik, SqlDbType.VarChar);
                db.Open();
                dr = db.ExecReader();
                if (dr.Read())
                {
                    SetWatch();
                    ConfirmStage(); // All done - push red
                } else
                {
                    Reset();
                    ShowSystemImage(this.SystemPic_KusovnikNOK);
                }
                db.Close();

                
            }
        }

        public void SetWatch()
        {
            StringBuilder query = new StringBuilder();
            query.AppendLine("select ID_Hodinky");
            query.AppendLine("from Geometrie_parametry");
            query.AppendLine("join Material on Material.mereni_geometrie = Geometrie_parametry.ID");
            query.AppendLine("where Material.kusovnik = @kusovnik");

            DBH db = new DBH(query.ToString());
            db.AddParameterTyped("kusovnik", this.Kusovnik, SqlDbType.VarChar);
            db.Open();
            SqlDataReader dr = db.ExecReader();
            if (dr.Read())
            {
                if (dr["ID_Hodinky"] != DBNull.Value)
                { 
                    this.Watch_DB = dr.GetInt32(dr.GetOrdinal("ID_Hodinky"));
                } else
                {
                    MessageBox.Show("V parametrech nejsou nastaveny hodinky. \n\r Kusovník: " + this.Kusovnik + "\n\r Resetuji cyklus.");
                    Reset();
                }
            }
            db.Close();
        }

        #endregion

        #region Set values

        // Convergence is repeated twice, 2nd time is normal KO/OK situation
        private void SetConvergence(string sbihavost)
        {
            this.Convergence = Convert.ToDouble(sbihavost);
            lbl_Sbihavost.Text = this.Convergence.ToString();

            if (!this.ConvergenceSave)
            {
                // Repeat!
                this.ConvergenceSave = true;
                MoveStage(Step.Convergence);
            } else
            {
                //Continue normaly
                if ((this.Convergence >= this.ConvergenceMin) && (this.Convergence <= this.ConvergenceMax))
                {
                    // Result: OK
                    lbl_Sbihavost.ForeColor = Color.DarkGreen;
                    MoveStage(Step.LeftMax);
                } else
                {
                    // Result: KO => Repeat
                    //lbl_Sbihavost.BackColor = Color.OrangeRed;
                    MoveStage(Step.Convergence);
                }
            }            
        }

        // If KO then repeat once
        public void SetRightMax()
        {
            double AmpValue = GetAmpValue();
            this.RightMax = AmpValue;
            lbl_Max_Prava_Elektro.Text = AmpValue.ToString();

            if (this.RightMaxSave)
            {
                Final = false;
                lbl_Max_Prava_Elektro.ForeColor = Color.Maroon;
                MoveStage(Step.Save);
            } else
            { 

                if ((this.RightMax >= this.RightMax_Min) && (this.RightMax <= this.RightMax_Max))
                {
                    lbl_Max_Prava_Elektro.ForeColor = Color.DarkGreen;
                    MoveStage(Step.Save);
                }
                else
                {
                    this.RightMaxSave = true;
                    this.RightMax = 0D;
                    //lbl_Max_Prava_Elektro.Text = "-";
                    MoveStage(Step.RightMax);
                }

            }
        }
        
        //If KO then repeat once
        public void SetLeftMax()
        {
            double AmpValue = GetAmpValue();
            this.LeftMax = AmpValue;
            lbl_Max_Leva_Elektro.Text = AmpValue.ToString();

            if (this.LeftMaxSave)
            {
                Final = false;
                lbl_Max_Leva_Elektro.ForeColor = Color.Maroon;
                MoveStage(Step.RightMax);
            } else
            { 

                if ((this.LeftMax >= this.LeftMax_Min) && (this.LeftMax <= this.LeftMax_Max))
                {
                    lbl_Max_Leva_Elektro.ForeColor = Color.DarkGreen;
                    MoveStage(Step.RightMax);
                }
                else
                {
                    this.LeftMaxSave = true;
                    this.LeftMax = 0D;
                    //lbl_Max_Leva_Elektro.Text = "-";
                    MoveStage(Step.LeftMax);
                }

            }
        }

        #endregion

        #region R/Y buttons

        // Delegate for the Red/Yellow button
        public delegate void Work();

        // Red - next stage
        public void ConfirmStage()
        {
            try
            {
                switch (this.step)
                {
                    case Step.Scan:
                        if (this.KusovnikDone && this.SerialDone && this.WorkerDone) // Move only if you have them all
                        {
                            MoveStage(Step.Amper);
                        }
                        break;
                    case Step.Amper:
                        MoveStage(Step.Convergence);
                        break;
                }
            } catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // Yellow - work => next stage
        public void ExecStage()
        {
            try
            { 
                switch (this.step)
                {
                    case Step.Convergence:

                        //MessageBox.Show("Watch_DB: " + this.Watch_DB.ToString() + "\r" +
                        //    "Watch_tag: " + this.Watch_tag.ToString() + "\r" +
                        //    "Watch2_tag: " + this.Watch2_tag.ToString());

                        // Reset watch
                        if (this.Watch_DB == this.Watch_tag)
                        {
                            this.Watch.Write("SET\r");
                        }

                        if (this.Watch_DB == this.Watch2_tag)
                        {
                            this.Watch2.Write("SET\r");
                        }

                        // 3rd Image
                        ShowImage("03");
                        break;

                    case Step.LeftMax:
                        if (this.GetLeftMax)
                        {
                            SetLeftMax();
                        } else
                        {
                            this.LeftMax = 0D;
                            lbl_Max_Leva_Elektro.Text = "-";
                            MoveStage(Step.RightMax);
                        }
                    
                        break;

                    case Step.RightMax:
                        if (this.GetRightMax)
                        {
                            SetRightMax();
                        } else
                        {
                            this.RightMax = 0D;
                            lbl_Max_Prava_Elektro.Text = "-";
                            MoveStage(Step.Save);
                        }                   
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        
        // All setup up for the new stage
        public void MoveStage(Step next)
        {
            this.step = next;

            switch (next)
            {
                case Step.Amper:
                    
                    lbl_Step.Text = "Nula stupňů";
                    this.GetAmp = false;
                    // Show 1st image
                    ShowImage("01");
                    // Check if you need to get AMP value and SHOW IT
                    StringBuilder query = new StringBuilder();
                    query.AppendLine("select top(1)");
                    query.AppendLine("nula_elektronicky_min,");
                    query.AppendLine("nula_elektronicky_max");
                    query.AppendLine("from Geometrie_parametry");
                    query.AppendLine("join Material on Material.mereni_geometrie = Geometrie_parametry.ID");
                    query.AppendLine("where (Material.kusovnik = @kusovnik)");
                    query.AppendLine("and (nula_elektronicky_min <> 999)");
                    query.AppendLine("and (nula_elektronicky_max <> 999) ");

                    SqlDataReader dr = null;
                    DBH db = new DBH(query.ToString());
                    db.AddParameterTyped("kusovnik", this.Kusovnik, SqlDbType.VarChar);
                    db.Open();
                    dr = db.ExecReader();
                    if (dr.Read())
                    {
                        this.ZeroValue_Max = dr.GetDouble(dr.GetOrdinal("nula_elektronicky_max"));
                        this.ZeroValue_Min = dr.GetDouble(dr.GetOrdinal("nula_elektronicky_min"));
                        lbl_nulovy_uhel_elektronicky_min.Text = ZeroValue_Min.ToString();
                        lbl_nulovy_uhel_elektronicky_max.Text = ZeroValue_Max.ToString();
                        this.GetAmp = true;
                    } else
                    {
                        lbl_nulovy_uhel_elektronicky_min.Text = "-";
                        lbl_nulovy_uhel_elektronicky_max.Text = "-";
                        this.GetAmp = false;
                    }
                    db.Close();
                    break;

                case Step.Convergence:
                    lbl_Step.Text = "Sbíhavost";
                    this.Convergence = 0D;
                    this.ConvergenceMin = 0D;
                    this.ConvergenceMax = 0D;

                    lbl_Sbihavost.ForeColor = Color.Maroon;

                    // lbl_Sbihavost.Text = "-";
                    // lbl_Sbihavost_min.Text = "-";
                    // lbl_Sbihavost_max.Text = "-";
                    

                    if (this.GetAmp && !this.ConvergenceSave) // Only first time
                    {
                        this.ZeroValue = 0D;
                        lbl_nulovy_uhel_elektronicky.Text = "-";
                        this.ZeroValue = GetAmpValue();
                        lbl_nulovy_uhel_elektronicky.Text = this.ZeroValue.ToString();

                        if ((this.ZeroValue >= this.ZeroValue_Min) && (this.ZeroValue <= this.ZeroValue_Max))
                        {
                            lbl_nulovy_uhel_elektronicky.ForeColor = Color.DarkGreen;

                        } else
                        {
                            Final = false;
                            lbl_nulovy_uhel_elektronicky.ForeColor = Color.Maroon;
                        }
                    }

                    // Show 2nd image
                    ShowImage("02");
                    lbl_NastaveniNuly.ForeColor = Color.DarkGreen;
                    SqlDataReader dr2 = null;
                        StringBuilder query2 = new StringBuilder();

                        query2.AppendLine("select top(1)");
                        query2.AppendLine("sbihavost_min,");
                        query2.AppendLine("sbihavost_max");
                        query2.AppendLine("from Geometrie_parametry");
                        query2.AppendLine("join Material on Material.mereni_geometrie = Geometrie_parametry.ID");
                        query2.AppendLine("where (Material.kusovnik = @kusovnik)");

                        DBH db2 = new DBH(query2.ToString());
                        db2.AddParameterTyped("kusovnik", this.Kusovnik, SqlDbType.VarChar);
                        db2.Open();
                        dr2 = db2.ExecReader();
                        if (dr2.Read())
                        {
                            this.ConvergenceMin = dr2.GetDouble(dr2.GetOrdinal("sbihavost_min"));
                            this.ConvergenceMax = dr2.GetDouble(dr2.GetOrdinal("sbihavost_max"));

                            lbl_Sbihavost_min.Text = this.ConvergenceMin.ToString();
                            lbl_Sbihavost_max.Text = this.ConvergenceMax.ToString();
                        }
                        db2.Close();

                    break;

                case Step.LeftMax:
                    lbl_Step.Text = "Levá MAX";
                    this.LeftMax_Min = 0D;
                    this.LeftMax_Max = 0D;
                    this.LeftMax = 0D;

                    //lbl_Max_Leva_Elektro_Min.Text = "-";
                    //lbl_Max_Leva_Elektro_Max.Text = "-";

                    SqlDataReader dr3 = null;
                    StringBuilder query3 = new StringBuilder();

                    query3.AppendLine("select top(1)");
                    query3.AppendLine("maximalni_levy_elektronicky_min,");
                    query3.AppendLine("maximalni_levy_elektronicky_max");
                    query3.AppendLine("from Geometrie_parametry");
                    query3.AppendLine("join Material on Material.mereni_geometrie = Geometrie_parametry.ID");
                    query3.AppendLine("where (Material.kusovnik = @kusovnik)");
                    query3.AppendLine("AND (maximalni_levy_elektronicky_min <> 999)");
                    query3.AppendLine("AND (maximalni_levy_elektronicky_max <> 999)");

                    DBH db3 = new DBH(query3.ToString());
                    db3.AddParameterTyped("kusovnik", this.Kusovnik, SqlDbType.VarChar);
                    db3.Open();
                    dr3 = db3.ExecReader();
                    if (dr3.Read())
                    {
                        ShowImage("04");
                        this.LeftMax_Min = dr3.GetDouble(dr3.GetOrdinal("maximalni_levy_elektronicky_min"));
                        this.LeftMax_Max = dr3.GetDouble(dr3.GetOrdinal("maximalni_levy_elektronicky_max"));
                        lbl_Max_Leva_Elektro_Min.Text = LeftMax_Min.ToString();
                        lbl_Max_Leva_Elektro_Max.Text = LeftMax_Max.ToString();
                        this.GetLeftMax = true;
                    } else
                    {
                        this.GetLeftMax = false;
                        MoveStage(Step.RightMax);
                    }

                    db3.Close();
                    break;

                case Step.RightMax:
                    lbl_Step.Text = "Pravá MAX";

                    this.RightMax_Min = 0D;
                    this.RightMax_Max = 0D;
                    this.RightMax = 0D;

                    //lbl_Max_Prava_Elektro_Min.Text = "-";
                    //lbl_Max_Prava_Elektro_Max.Text = "-";

                    SqlDataReader dr4 = null;
                    StringBuilder query4 = new StringBuilder();

                    query4.AppendLine("select top(1)");
                    query4.AppendLine("maximalni_pravy_elektronicky_min,");
                    query4.AppendLine("maximalni_pravy_elektronicky_max");
                    query4.AppendLine("from Geometrie_parametry");
                    query4.AppendLine("join Material on Material.mereni_geometrie = Geometrie_parametry.ID");
                    query4.AppendLine("where (Material.kusovnik = @kusovnik)");
                    query4.AppendLine("AND (maximalni_pravy_elektronicky_min <> 999)");
                    query4.AppendLine("AND (maximalni_pravy_elektronicky_max <> 999)");

                    DBH db4 = new DBH(query4.ToString());
                    db4.AddParameterTyped("kusovnik", this.Kusovnik, SqlDbType.VarChar);
                    db4.Open();
                    dr4 = db4.ExecReader();
                    if (dr4.Read())
                    {
                        ShowImage("05");
                        this.RightMax_Min = dr4.GetDouble(dr4.GetOrdinal("maximalni_pravy_elektronicky_min"));
                        this.RightMax_Max = dr4.GetDouble(dr4.GetOrdinal("maximalni_pravy_elektronicky_max"));
                        lbl_Max_Prava_Elektro_Min.Text = RightMax_Min.ToString();
                        lbl_Max_Prava_Elektro_Max.Text = RightMax_Max.ToString();
                        this.GetRightMax = true;
                    }
                    else
                    {
                        this.GetRightMax = false;
                        MoveStage(Step.Save);
                    }

                    db4.Close();

                    break;

                case Step.Save:
                    lbl_Step.Text = "Uložit";
                    SaveAll();
                    break;
            }
        }

        #endregion

        #region Image

        public void ResetImage()
        {
            pictureBox1.Image = Geometrie.Properties.Resources.zf_logo;
        }

        public void ShowImage(string num)
        {
            string path = "";
            try
            {
                path = Path.Combine(this.FolderPath, this.Kusovnik, num + ".jpg");
                pictureBox1.Image = new Bitmap(path);
            } catch (Exception ex)
            {
                AddError("Chyba načtení obrázku: " + path);
                throw;
            }
            
        }

        #endregion

        #region System

        private void resetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("Opravdu resetovat cyklus?", "RESET", MessageBoxButtons.YesNo);

            if (result == DialogResult.Yes)
            {
                this.Final = false;
                SaveAll(true);
            }
        }

        // Gotta get back...back to the past.....Samurai Jack...
        private void Reset()
        {
            resetToolStripMenuItem.Enabled = false;

            ShowResults();
            //Step
            ResetImage();
            this.step = Step.Scan;
            lbl_Step.Text = "Skenování";

            // GUI colors
            lbl_Sbihavost.ForeColor = Color.Maroon;
            lbl_nulovy_uhel_elektronicky.ForeColor = Color.Maroon;
            lbl_Max_Leva_Elektro.ForeColor = Color.Maroon;
            lbl_Max_Prava_Elektro.ForeColor = Color.Maroon;
            lbl_NastaveniNuly.ForeColor = Color.Maroon;

            // GUI values
            txt_Kusovnik.Text = "";
            txt_SerioveCislo.Text = "";

            lbl_nulovy_uhel_elektronicky.Text = "-";
            lbl_nulovy_uhel_elektronicky_min.Text = "-";
            lbl_nulovy_uhel_elektronicky_max.Text = "-";

            lbl_Max_Prava_Elektro.Text = "-";
            lbl_Max_Prava_Elektro_Min.Text = "-";
            lbl_Max_Prava_Elektro_Max.Text = "-";

            lbl_Max_Leva_Elektro.Text = "-";
            lbl_Max_Leva_Elektro_Min.Text = "-";
            lbl_Max_Leva_Elektro_Max.Text = "-";

            lbl_Sbihavost.Text = "-";
            lbl_Sbihavost_min.Text = "-";
            lbl_Sbihavost_max.Text = "-";

            // Variables
            Kusovnik = null;
            Serial = null;
            Worker = null;

            Kusovnik_tmp = null;
            Serial_tmp = null;
            Worker_tmp = null;

            KusovnikDone = false;
            SerialDone = false;
            WorkerDone = false;

            GetAmp = false;
            GetLeftMax = false;
            GetRightMax = false;

            ZeroValue = 0D;
            ZeroValue_Min = 0D;
            ZeroValue_Max = 0D;

            LeftMax_Min = 0D;
            LeftMax_Max = 0D;
            LeftMax = 0D;

            RightMax_Min = 0D;
            RightMax_Max = 0D;
            RightMax = 0D;

            ConvergenceMin = 0D;
            ConvergenceMax = 0D;
            Convergence = 0D;

            LeftMaxSave = false;
            RightMaxSave = false;
            ConvergenceSave = false;

            this.Watch_DB = 0;
            //this.Watch_tag = 0;
            //this.Watch2_tag = 0;

            this.Final = true;

            this.Error = "";

        }

        //Gets value from Ampermeter
        public double GetAmpValue()
        {
            // Clear buffer and wait for new data
            this.Amp.DiscardInBuffer();
            Thread.Sleep(500);

            string proud = this.Amp.ReadExisting();
            string tmp = proud.Replace("\n", "-");
            string tmp2 = tmp.Substring(0, tmp.LastIndexOf("-"));
            string tmp3 = tmp2.Substring(tmp2.LastIndexOf("-") + 1);
            double value = Convert.ToDouble(tmp3)/100;

            return value;
        }

        private void AddError(string msg)
        {
            this.Error += msg + "\r";
        }

        public Form1()
        {
            InitializeComponent();
        }

        #endregion

    }

}
