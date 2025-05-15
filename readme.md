# Usage
1. Download this repository into your project directory. 
You will probably prefer to add it as git submodule.

`git submodule add https://github.com:RocketRide9/SparkCL.git`\
or\
`git submodule add ../SparkCL.git`

2. Add folowing to your `.csproj` file. Path to `SparkCL.csproj` might be different depending on your project structure.
```xml
<ItemGroup>
  <ProjectReference Include="SparkCL/SparkCL.csproj" />
</ItemGroup> 
```
Tip: You can add something similar to `.csproj` so you can refer to OpenCL sources by their filename, if you are using Visual Studio or VSCode.
```xml
<ItemGroup>
  <None Update="Kernels.clcpp">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
  <None Update="BLAS.cl">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```
