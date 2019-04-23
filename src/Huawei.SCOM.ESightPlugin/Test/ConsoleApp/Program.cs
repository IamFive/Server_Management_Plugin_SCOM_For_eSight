using System;

namespace ConsoleApp
{

    using Huawei.SCOM.ESightPlugin.RESTeSightLib.Helper;

    class Program
    {
        static void Main(string[] args)
        {
            ESightApplianceOperationConsumer.Instance.Consume();
        }
    }
}
