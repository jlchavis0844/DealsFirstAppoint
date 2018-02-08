using System;

namespace DealsFirstAppoint {
    public class Deal {

        public int id { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
        public string name { get; set; }
        public int creator_id { get; set; }
        public int source_id { get; set; }
        public DateTime last_stage_change_at { get; set; }
        public int last_stage_change_by_id { get; set; }
        public string owner_name { get; set; }
        public int owner_id { get; set; }
        public int stage_id { get; set; }
        public string stageName { get; set; }
        public DateTime last_activity_at { get; set; }
        public string estimated_close_date { get; set; }

        public Deal(int id, DateTime created_at, DateTime updated_at, DateTime last_stage_change_at, 
            int last_stage_change_by_id, string owner_name, int owner_id, int stage_id, string stageName, string estimated_close_date) {
            this.id = id;
            this.created_at = created_at;
            this.updated_at = updated_at;
            this.last_stage_change_at = last_stage_change_at;
            this.last_stage_change_by_id = last_stage_change_by_id;
            this.owner_name = owner_name;
            this.owner_id = owner_id;
            this.stage_id = stage_id;
            this.stageName = stageName;
            this.estimated_close_date = estimated_close_date;
        }

        public Deal() {

        }
    }
}