// See https://aka.ms/new-console-template for more information

using BprpDetector.core;
using BprpDetector.log;

var logger = new ConsoleLogger(); // 你需要实现ILogger接口
var capture = new GamePacketCapture();

try
{
    // 获取所有网卡
    var devices = capture.GetAllNetworkDevices();
            
    Console.WriteLine("Please select a network device:");
    for (int i = 0; i < devices.Count; i++)
    {
        Console.WriteLine($"{i}: {devices[i].Description}");
    }

    Console.Write("Enter device number: ");
    if (int.TryParse(Console.ReadLine(), out int deviceIndex))
    {
        // 开始抓包
        await capture.StartCapture(deviceIndex);
    }
    else
    {
        Console.WriteLine("Invalid device number");
    }
}
catch (Exception ex)
{
    logger.Error($"Error: {ex.Message}");
}
finally
{
    capture.Stop();
}