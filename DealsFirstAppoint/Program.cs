using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using unirest_net.http;

namespace DealsFirstAppoint {
    class Program {
        public static Dictionary<int, string> ownersData = new Dictionary<int, string>();
        public static Dictionary<int, string> stageData = new Dictionary<int, string>();
        public static StreamWriter log;
        public static List<Owner> owners;
        public static List<Deal> conList;
        public static string token = "";
        private static Random random = new Random();
        private static string connString = "Data Source=RALIMSQL1;Initial Catalog=CAMSRALFG;Integrated Security=SSPI;";
        private static string line = @"INSERT INTO [CAMSRALFG].[dbo].[StageChanges] ([dealID], [set_at], [set_by], [insertedby], " +
            "[created_at],[Owner_name], [owner_id], [stage_id], [stageName]) VALUES (@id, @last_stage_change_at, " + 
            "@last_stage_change_by_id, @me, @created_at,@owner_name, @owner_id, @stage_id, @stageName);";

        static void Main(string[] args) {
            string startURL = @"https://api.getbase.com/v2/deals?per_page=100&sort_by=updated_at:desc";
            owners = new List<Owner>();
            conList = new List<Deal>();
            var fs = new FileStream(@"C:\apps\NiceOffice\token", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            DateTime now = DateTime.Now;
            using (var sr = new StreamReader(fs)) {
                token = sr.ReadToEnd();
            }
            string logPath = @"\\NAS3\NOE_Docs$\RALIM\Logs\Base\NewApps_";
            logPath += now.ToString("ddMMyyyy") + ".txt";

            if (!File.Exists(logPath)) {
                using(StreamWriter sw = File.CreateText(logPath)) {
                    sw.WriteLine("Creating 1st appoitment log file for " + now.ToString("ddMMyyyy") + " at " + now.ToString());
                }
            }

            log = File.AppendText(logPath);
            log.WriteLine("Starting check");
            Console.WriteLine("Starting check");

            string me = Environment.UserDomainName.ToString() + @"\" + Environment.UserName;
            SetStageData();
            SetOwnerData();
            DateTime limitDate = GetLastDate();

            string testJSON = Get(startURL, token);
            DateTime lastWeek = DateTime.Now.Date.AddDays(-7);
            
            log.WriteLine(limitDate + " to " + now);
            Console.WriteLine(limitDate + " to " + now);
            JObject jsonObj = JObject.Parse(testJSON) as JObject;
            var jArr = jsonObj["items"] as JArray;

            foreach (var v in jArr) {
                var data = v["data"];

                int id = Convert.ToInt32(data["id"]);
                DateTime created_at = Convert.ToDateTime(data["created_at"]).ToLocalTime();
                DateTime updated_at = Convert.ToDateTime(data["updated_at"]).ToLocalTime();
                DateTime last_stage_change_at = Convert.ToDateTime(data["last_stage_change_at"]).ToLocalTime();

                int stage_id = Convert.ToInt32(data["stage_id"]);
                string stageName = stageData[stage_id];

                int last_stage_change_by_id = 0;
                if (data["last_stage_change_by_id"] != null && data["last_stage_change_by_id"].ToString() != "") {
                    last_stage_change_by_id = Convert.ToInt32(data["last_stage_change_by_id"]);
                }

                int owner_id = 0;
                if(data["owner_id"] != null && data["owner_id"].ToString() != "") {
                    owner_id = Convert.ToInt32(data["owner_id"]);
                }

                string owner_name = ownersData[owner_id].ToString();

                if (last_stage_change_at >= limitDate) {
                    if (CheckForDuplicates(id, stage_id) == false) {
                        conList.Add(new Deal(id, created_at, updated_at, last_stage_change_at, last_stage_change_by_id, 
                            owner_name, owner_id, stage_id, stageName));
                    }
                }
                else if(updated_at < lastWeek){
                    log.WriteLine(last_stage_change_at + " is too old, breaking");
                    Console.WriteLine(last_stage_change_at + " is too old, breaking");
                    break;
                }
            }

            string nextURL = jsonObj["meta"]["links"]["next_page"].ToString();
            while (Convert.ToDateTime(jArr.Last["data"]["updated_at"]) >= limitDate) {
                log.WriteLine(Convert.ToDateTime(jArr.Last["data"]["updated_at"]) + " >= " + limitDate);
                Console.WriteLine(Convert.ToDateTime(jArr.Last["data"]["updated_at"]) + " >= " + limitDate);
                jsonObj = JObject.Parse(Get(nextURL, token)) as JObject;
                nextURL = jsonObj["meta"]["links"]["next_page"].ToString();
                jArr = jsonObj["items"] as JArray;

                foreach (var v in jArr) {
                    var data = v["data"];
                    int id = Convert.ToInt32(data["id"]);
                    DateTime created_at = Convert.ToDateTime(data["created_at"]).ToLocalTime();
                    DateTime updated_at = Convert.ToDateTime(data["updated_at"]).ToLocalTime();
                    DateTime last_stage_change_at = Convert.ToDateTime(data["last_stage_change_at"]).ToLocalTime();

                    int stage_id = Convert.ToInt32(data["stage_id"]);
                    string stageName = stageData[stage_id];

                    int last_stage_change_by_id = 0;
                    if (data["last_stage_change_by_id"] != null && data["last_stage_change_by_id"].ToString() != "") {
                        last_stage_change_by_id = Convert.ToInt32(data["last_stage_change_by_id"]);
                    }

                    int owner_id = 0;
                    if (data["owner_id"] != null && data["owner_id"].ToString() != "") {
                        owner_id = Convert.ToInt32(data["owner_id"]);
                    }

                    string owner_name = ownersData[owner_id].ToString();

                    if (last_stage_change_at > limitDate) {
                        if (CheckForDuplicates(id, stage_id) == false) {
                            conList.Add(new Deal(id, created_at, updated_at, last_stage_change_at, last_stage_change_by_id,
                                owner_name, owner_id, stage_id, stageName));
                        }
                    }
                    else if (updated_at < lastWeek) {
                        log.WriteLine(last_stage_change_at + " is too old, breaking");
                        Console.WriteLine(last_stage_change_at + " is too old, breaking");
                        break;
                    }
                }
            }

            if(conList.Count <= 0) {
                log.WriteLine("Empty results after 7 days, quitting...");
                Console.WriteLine("Empty results after 7 days, quitting...");
                log.Close();
                Environment.Exit(0);
            }

            //StreamWriter file = new StreamWriter("H:\\Desktop\\1stAppDataLine.csv");
            //log.WriteLine("id,created_at, updated_at, last_stage_change_at,last_stage_change_by_id,owner_name, owner_id");
            //Console.WriteLine("id,created_at, updated_at, last_stage_change_at,last_stage_change_by_id,owner_name, owner_id");

            List<Object[]> inserts = new List<Object[]>();

            foreach (var item in conList) {
                Object[] tempObj = {item.id, item.created_at, item.last_stage_change_at, item.last_stage_change_by_id,
                item.owner_name, item.owner_id, item.stage_id, item.stageName};
                inserts.Add(tempObj);
                log.WriteLine(item.id + ", " + item.created_at + ", " + item.updated_at + ", " + item.last_stage_change_at + 
                    ", " + item.last_stage_change_by_id + ", " + item.owner_name + ", " + item.owner_id 
                    + ", " + item.stage_id + ", " + item.stageName);
                Console.WriteLine(item.id + ", " + item.created_at + ", " + item.updated_at + ", " + item.last_stage_change_at +
                    ", " + item.last_stage_change_by_id + ", " + item.owner_name + ", " + item.owner_id
                    + ", " + item.stage_id + ", " + item.stageName);
            }

            //file.Flush();
            //file.Close();

            string connString = "Data Source=RALIMSQL1;Initial Catalog=CAMSRALFG;Integrated Security=SSPI;";

            using (SqlConnection connection = new SqlConnection(connString)) {

                foreach (Object[] oArr in inserts) {
                    using (SqlCommand command = new SqlCommand(line, connection)) {
                        command.Parameters.Add("@id", SqlDbType.Int).Value = oArr[0];
                        command.Parameters.Add("@created_at", SqlDbType.DateTime).Value = oArr[1];
                        command.Parameters.Add("@last_stage_change_at", SqlDbType.DateTime).Value = oArr[2];
                        command.Parameters.Add("@last_stage_change_by_id", SqlDbType.Int).Value = oArr[3];
                        command.Parameters.Add("@owner_name", SqlDbType.NVarChar).Value = oArr[4];
                        command.Parameters.Add("@owner_id", SqlDbType.Int).Value = oArr[5];
                        command.Parameters.Add("@me", SqlDbType.NVarChar).Value = me;
                        command.Parameters.Add("@stage_id", SqlDbType.Int).Value = oArr[6];
                        command.Parameters.Add("@stageName", SqlDbType.NVarChar).Value = oArr[7];

                        try {
                            connection.Open();
                            int result = command.ExecuteNonQuery();

                            if (result < 0) {
                                log.WriteLine("INSERT failed for " + command.ToString());
                                Console.WriteLine("INSERT failed for " + command.ToString());
                            }
                        }
                        catch (Exception ex) {
                            log.WriteLine(ex);
                            Console.WriteLine(ex);
                            log.Flush();
                        }
                        finally {
                            connection.Close();
                        }
                    }
                }
            }

            log.WriteLine("Done!");
            Console.WriteLine("Done!");
            log.Close();
            //log.ReadLine();
        }

        public static string Get(string url, string token) {
            string body = "";
            try {
                HttpResponse<string> jsonReponse = Unirest.get(url)
                    .header("accept", "application/json")
                    .header("Authorization", "Bearer " + token)
                    .asJson<string>();
                body = jsonReponse.Body.ToString();
                return body;
            }
            catch (Exception ex) {
                log.WriteLine(ex);
                log.WriteLine(ex);

                log.Flush();
                return body;
            }
        }

        public static void SetOwnerData() {
            string testJSON = Get(@"https://api.getbase.com/v2/users?per_page=100&sort_by=created_at&status=active", token);
            JObject jObj = JObject.Parse(testJSON) as JObject;
            JArray jArr = jObj["items"] as JArray;
            foreach (var obj in jArr) {
                var data = obj["data"];
                int tID = Convert.ToInt32(data["id"]);
                string tName = data["name"].ToString();
                ownersData.Add(tID, tName);
            }
        }

        public static void SetStageData() {
            string testJSON = Get(@"https://api.getbase.com/v2/stages?per_page=100", token);
            JObject jObj = JObject.Parse(testJSON) as JObject;
            JArray jArr = jObj["items"] as JArray;
            stageData.Add(0, "Unknown");

            foreach (var obj in jArr) {
                var data = obj["data"];
                int tID = Convert.ToInt32(data["id"]);
                string tName = data["name"].ToString();
                stageData.Add(tID, tName);
            }
        }

        public static DateTime GetLastDate() {
            DateTime limit = new DateTime();
            using (SqlConnection connection = new SqlConnection(connString)) {
                string sqlStr = "SELECT MAX([set_at]) FROM [CAMSRALFG].[dbo].[StageChanges];";
                using (SqlCommand command = new SqlCommand(sqlStr, connection)) {

                    try {
                        connection.Open();

                        SqlDataReader result = command.ExecuteReader();

                        while (result.Read()) {
                            if (!result.IsDBNull(0)) {
                                limit = result.GetDateTime(0);
                            }
                        }

                        if (limit == DateTime.MinValue) {
                            return DateTime.Now.Date.AddDays(-7);
                        }
                        else log.WriteLine("Found max date of " + limit);
                    } catch (Exception ex) {
                        log.WriteLine(ex);
                        Console.WriteLine(ex);

                        log.Flush();
                    } finally {
                        connection.Close();
                    }
                }

            }
            return limit;
        }

        public static bool CheckForDuplicates(int id, int stage_id) {
            int cnt_of_id = -1;
            using (SqlConnection connection = new SqlConnection(connString)) {
                string sqlStr = "SELECT COUNT(*) FROM [CAMSRALFG].[dbo].[StageChanges] WHERE dealID = " + id + " AND [stage_id] = " + stage_id +";";
                using (SqlCommand command = new SqlCommand(sqlStr, connection)) {

                    try {
                        connection.Open();

                        SqlDataReader result = command.ExecuteReader();

                        while (result.Read()) {
                            cnt_of_id = result.GetInt32(0);
                        }

                        if (cnt_of_id == -1) {
                            throw new Exception("COUNT failed");
                        }
                        else if (cnt_of_id > 0) {
                            log.WriteLine("FOUND previous records: " + cnt_of_id);
                        }
                    }
                    catch (Exception ex) {
                        log.WriteLine(ex);
                        Console.WriteLine(ex);

                        log.Flush();
                    }
                    finally {
                        connection.Close();
                    }
                }

            }
            if (cnt_of_id > 0) {
                return true;
            }
            else return false;
        }

        public static string RandomString(int length) {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
