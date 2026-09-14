using System;
using System.ServiceModel;
using Autodesk.RevitServer.Enterprise.Common.ClientServer.DataContract.SessionToken;
using Autodesk.RevitServer.Enterprise.Common.ClientServer.ServiceContract.Model;

class Test {
    static void Main() {
        string[] suffixes = new string[] { "tcpbuffer", "tcpstreamed", "" };
        bool[] auths = new bool[] { true, false };
        string host = "revit_prokshino.delkons.local";
        string year = "2022";

        foreach (bool auth in auths) {
            foreach (string suffix in suffixes) {
                string url = string.IsNullOrEmpty(suffix) 
                    ? ("net.tcp://" + host + "/ModelService" + year + "/ModelService.svc")
                    : ("net.tcp://" + host + "/ModelService" + year + "/ModelService.svc/" + suffix);

                Console.WriteLine("\n--- Testing url=" + url + ", auth=" + auth + " ---");
                try {
                    NetTcpBinding binding = new NetTcpBinding();
                    binding.TransferMode = TransferMode.Buffered;
                    binding.MaxReceivedMessageSize = 67108864L;
                    binding.MaxBufferSize = 67108864;
                    binding.SendTimeout = TimeSpan.FromSeconds(3);
                    binding.OpenTimeout = TimeSpan.FromSeconds(3);

                    if (auth) {
                        binding.Security.Mode = SecurityMode.Transport;
                        binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;
                    } else {
                        binding.Security.Mode = SecurityMode.None;
                    }

                    ChannelFactory<IModelService> factory = new ChannelFactory<IModelService>(binding, new EndpointAddress(url));
                    IModelService channel = factory.CreateChannel();
                    ((IClientChannel)channel).Open();
                    Console.WriteLine("  SUCCESS: Channel opened!");
                    ((IClientChannel)channel).Close();
                    return;
                } catch (Exception ex) {
                    Console.WriteLine("  FAILED: " + ex.GetType().Name + ": " + ex.Message);
                    if (ex.InnerException != null) {
                        Console.WriteLine("    Inner: " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message);
                    }
                }
            }
        }
    }
}
