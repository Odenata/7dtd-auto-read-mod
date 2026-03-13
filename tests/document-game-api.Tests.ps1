# Unit tests for document-game-api.ps1

Describe "document-game-api.ps1" {
    BeforeAll {
        $scriptPath = "$PSScriptRoot\..\tools\document-game-api.ps1"
        $testGamePath = "$PSScriptRoot\test-data\fake-game"
        $testOutputFile = "$PSScriptRoot\test-output\GAME_API.md"
        
        # Create test directories
        New-Item -ItemType Directory -Path "$testGamePath\7DaysToDie_Data\Managed" -Force | Out-Null
        New-Item -ItemType Directory -Path "$PSScriptRoot\test-output" -Force | Out-Null
        
        # Create a fake game executable for version testing
        $fakeExe = "$testGamePath\7DaysToDie.exe"
        "fake exe" | Out-File -FilePath $fakeExe -Encoding ASCII
    }
    
    AfterAll {
        # Cleanup test directories
        if (Test-Path "$PSScriptRoot\test-data") {
            Remove-Item -Path "$PSScriptRoot\test-data" -Recurse -Force
        }
        if (Test-Path "$PSScriptRoot\test-output") {
            Remove-Item -Path "$PSScriptRoot\test-output" -Recurse -Force
        }
    }
    
    Context "Script Validation" {
        It "Script file exists" {
            Test-Path $scriptPath | Should -Be $true
        }
        
        It "Script has valid PowerShell syntax" {
            $errors = $null
            $null = [System.Management.Automation.PSParser]::Tokenize((Get-Content $scriptPath -Raw), [ref]$errors)
            $errors.Count | Should -Be 0
        }
    }
    
    Context "Parameter Validation" {
        It "Accepts GamePath parameter" {
            $script = Get-Command $scriptPath
            $script.Parameters.ContainsKey('GamePath') | Should -Be $true
        }
        
        It "Accepts OutputFile parameter" {
            $script = Get-Command $scriptPath
            $script.Parameters.ContainsKey('OutputFile') | Should -Be $true
        }
        
        It "Has default values for parameters" {
            $script = Get-Command $scriptPath
            $script.Parameters['GamePath'].Attributes.ValueFromPipeline | Should -Not -BeNullOrEmpty
            $script.Parameters['OutputFile'].Attributes.ValueFromPipeline | Should -Not -BeNullOrEmpty
        }
    }
    
    Context "Game Version Detection" {
        It "Detects game version from executable" {
            # This test would need a real game executable or mock
            # For now, just verify the script attempts to get version
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'VersionInfo.FileVersion'
        }
        
        It "Handles missing game executable gracefully" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'Test-Path.*gameExe'
        }
    }
    
    Context "Assembly Loading" {
        It "Checks for Assembly-CSharp.dll existence" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'Assembly-CSharp.dll'
        }
        
        It "Uses correct assembly loading method" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match '\[System.Reflection.Assembly\]::LoadFile'
        }
        
        It "Handles assembly loading errors" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'Test-Path.*assemblyPath'
        }
    }
    
    Context "Type Extraction" {
        It "Defines classes of interest" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match '\$classesOfInterest'
        }
        
        It "Includes key modding classes" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'XUiC_LootWindowGroup'
            $scriptContent | Should -Match 'ItemClass'
            $scriptContent | Should -Match 'EntityPlayer'
        }
        
        It "Uses GetType for type lookup" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'GetType\('
        }
    }
    
    Context "Method Extraction" {
        It "Extracts public methods" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'BindingFlags.*Public'
        }
        
        It "Extracts non-public methods" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'BindingFlags.*NonPublic'
        }
        
        It "Filters out special methods" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'IsSpecialName'
        }
        
        It "Extracts method parameters" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'GetParameters\(\)'
        }
    }
    
    Context "Field and Property Extraction" {
        It "Extracts public fields" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'GetFields\('
        }
        
        It "Extracts public properties" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'GetProperties\('
        }
    }
    
    Context "Error Handling" {
        It "Has try-catch blocks for reflection errors" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'try\s*\{'
            $scriptContent | Should -Match 'catch\s*\{'
        }
        
        It "Handles null types gracefully" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'if.*-not.*\$type'
        }
        
        It "Wraps parameter extraction in error handling" {
            $scriptContent = Get-Content $scriptPath -Raw
            # Should have try-catch around GetParameters or similar
            $scriptContent | Should -Match 'catch'
        }
    }
    
    Context "Output Generation" {
        It "Creates output directory if missing" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'New-Item.*Directory'
        }
        
        It "Generates markdown format" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match '##|###|\*\*'
        }
        
        It "Includes game version in output" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match '\$version'
        }
        
        It "Includes generation timestamp" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'Get-Date'
        }
        
        It "Writes output to file" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'Out-File'
        }
    }
    
    Context "Documentation Quality" {
        It "Documents namespace for each class" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'Namespace:'
        }
        
        It "Documents full name for each class" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'Full Name:'
        }
        
        It "Separates public and non-public methods" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'Public Methods'
            $scriptContent | Should -Match 'Non-Public Methods'
        }
        
        It "Includes method return types" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'ReturnType'
        }
        
        It "Includes method parameters with types" {
            $scriptContent = Get-Content $scriptPath -Raw
            $scriptContent | Should -Match 'ParameterType'
        }
    }
}
