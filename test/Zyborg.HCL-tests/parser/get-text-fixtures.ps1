
##
## 1. copy dir listing text from:
##    https://github.com/hashicorp/hcl/tree/master/hcl/parser/test-fixtures
## 2. Regex replace with: s/\.hcl\s+.+/.hcl/
## 3. Prefix each file with and fetch:
#3    https://raw.githubusercontent.com/hashicorp/hcl/master/hcl/parser/test-fixtures/
##    

$githubPrefix = "https://raw.githubusercontent.com/hashicorp/hcl/master/hcl/parser/test-fixtures/"
$files = @(
    ,"array_comment.hcl"
    ,"array_comment_2.hcl"
    ,"assign_colon.hcl"
    ,"assign_deep.hcl"
    ,"comment.hcl"
    ,"comment_crlf.hcl"
    ,"comment_lastline.hcl"
    ,"comment_single.hcl"
    ,"complex.hcl"
    ,"complex_crlf.hcl"
    ,"complex_key.hcl"
    ,"empty.hcl"
    ,"git_crypt.hcl"
    ,"key_without_value.hcl"
    ,"list.hcl"
    ,"list_comma.hcl"
    ,"missing_braces.hcl"
    ,"multiple.hcl"
    ,"object_key_assign_without_value.hcl"
    ,"object_key_assign_without_value2.hcl"
    ,"object_key_assign_without_value3.hcl"
    ,"object_key_without_value.hcl"
    ,"object_list_comma.hcl"
    ,"old.hcl"
    ,"structure.hcl"
    ,"structure_basic.hcl"
    ,"structure_empty.hcl"
    ,"types.hcl"
    ,"unterminated_object.hcl"
    ,"unterminated_object_2.hcl"
)

$targetDir = "$(pwd)/test-fixtures"

if (-not (Test-Path -PathType Container $targetDir)) {
    mkdir $targetDir
}

foreach ($f in $files) {
    $uri = $githubPrefix + $f
    $out = "$($targetDir)/$($f)"
    echo "Fetching '$($uri)' to '$($out)'"
    Invoke-WebRequest -Uri $uri -OutFile $out
}