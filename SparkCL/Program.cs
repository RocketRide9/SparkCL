using OCLHelper;

namespace SparkCL;

public class Program
{
    OCLHelper.Program program;

    public Program(string fileName)
    {
        program = OCLHelper.Program.FromFilename(Core.context!, Core.device!, fileName);
    }

    public SparkCL.Kernel GetKernel(string kernelName, NDRange globalWork, NDRange localWork)
    {
        var oclKernel = new OCLHelper.Kernel(program, kernelName);
        return new Kernel(oclKernel, globalWork, localWork);
    }
}
