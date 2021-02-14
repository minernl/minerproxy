using System.Collections.Generic;

namespace MinerProxy.JsonProtocols
{
    public class VapServerRootObject
    {
        public int id { get; set; }
        public string jsonrpc { get; set; }
        public List<string> result { get; set; }
    }

    public class VapError
    {
        public int code { get; set; }
        public string message { get; set; }
    }

    public class VapServerRootObjectBool
    {
        public int? id { get; set; }
        public string jsonrpc { get; set; }
        public bool? result { get; set; }
        public VapError error { get; set; }
    }

    public class VapServerRootObjectError
    {
        public int? id { get; set; }
        public string jsonrpc { get; set; }
        public bool? result { get; set; }
        public string error { get; set; }
    }

    public class VapClientRootObject
    {
        public string worker { get; set; }
        public string jsonrpc { get; set; }
        public List<string> @params { get; set; }
        public int? id { get; set; }
        public string method { get; set; }
    }
}
