// using Core.Application;
// using Core.Application.Services;
//
// namespace EMS.Tools.RpcClients;
//
// public static class Rpc
// {
//     public static async Task Run()
//     {
//         var handshakeValidator = new HandshakeValidatorService();
//         var network = new HandshakeService(handshakeValidator);
//
//         var result = await network.Handshake(CoreApplicationConstants.Apps.WITNESS, new CancellationToken());
//         var host = $"http://{result}:11337";
//
//         var clients = new List<ArrivelistClient>();
//         for (var i = 0; i < 50; i++)
//         {
//             var client = new ArrivelistClient();
//             client.Configure(host);
//             await client.Start();
//             clients.Add(client);
//         }
//
//         while (true)
//         {
//             foreach (var client in clients)
//             {
//                 await client.Get();
//             }
//             await Task.Delay(TimeSpan.FromSeconds(5));
//         }
//     }
// }
