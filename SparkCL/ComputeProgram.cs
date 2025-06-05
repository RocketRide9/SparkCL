using OCLHelper;

namespace SparkCL;

public class ComputeProgram
{
    Program program;

    public ComputeProgram(string fileName)
    {
        program = Program.FromFilename(Core.context!, Core.device!, fileName);
    }

    public SparkCL.Kernel GetKernel(string kernelName, NDRange globalWork, NDRange localWork)
    {
        var oclKernel = new OCLHelper.Kernel(program, kernelName);
        return new Kernel(oclKernel, globalWork, localWork);
    }
}
