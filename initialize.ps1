# Download IKVM.NET Binaries
& .\.nuget\NuGet.exe restore Cea.sln

# Download ASM
Invoke-WebRequest -Uri http://central.maven.org/maven2/org/ow2/asm/asm/5.0.3/asm-5.0.3.jar -OutFile asm.jar
Invoke-WebRequest -Uri http://central.maven.org/maven2/org/ow2/asm/asm-commons/5.0.3/asm-commons-5.0.3.jar -OutFile asm-commons.jar
Invoke-WebRequest -Uri http://central.maven.org/maven2/org/ow2/asm/asm-tree/5.0.3/asm-tree-5.0.3.jar -OutFile asm-tree.jar

# Convert with IKVM.NET
& .\packages\IKVM-WithExes.7.3.4830.1\tools\ikvmc.exe -target:library -out:asm.dll asm.jar asm-commons.jar asm-tree.jar
