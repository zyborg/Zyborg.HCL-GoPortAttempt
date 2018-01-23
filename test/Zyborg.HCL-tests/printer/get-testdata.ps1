
##
## 1. copy dir listing text from:
##    https://github.com/hashicorp/hcl/tree/master/hcl/printer/testdata
## 2. Regex replace with: s/\.golden\s+.+/.golden/
##    Regex replace with: s/\.input\s+.+/.input/
## 3. Prefix each file with and fetch:
#3    https://raw.githubusercontent.com/hashicorp/hcl/master/hcl/printer/testdata/
##    

$githubPrefix = "https://raw.githubusercontent.com/hashicorp/hcl/master/hcl/printer/testdata/"
$files = @(
    ,"comment.golden"
    ,"comment.input"
    ,"comment_aligned.golden"
    ,"comment_aligned.input"
    ,"comment_array.golden"
    ,"comment_array.input"
    ,"comment_crlf.input"
    ,"comment_end_file.golden"
    ,"comment_end_file.input"
    ,"comment_multiline_indent.golden"
    ,"comment_multiline_indent.input"
    ,"comment_multiline_no_stanza.golden"
    ,"comment_multiline_no_stanza.input"
    ,"comment_multiline_stanza.golden"
    ,"comment_multiline_stanza.input"
    ,"comment_newline.golden"
    ,"comment_newline.input"
    ,"comment_object_multi.golden"
    ,"comment_object_multi.input"
    ,"comment_standalone.golden"
    ,"comment_standalone.input"
    ,"complexhcl.golden"
    ,"complexhcl.input"
    ,"empty_block.golden"
    ,"empty_block.input"
    ,"list.golden"
    ,"list.input"
    ,"list_comment.golden"
    ,"list_comment.input"
    ,"list_of_objects.golden"
    ,"list_of_objects.input"
    ,"multiline_string.golden"
    ,"multiline_string.input"
    ,"object_singleline.golden"
    ,"object_singleline.input"
    ,"object_with_heredoc.golden"
    ,"object_with_heredoc.input"
)

$targetDir = "$(pwd)/testdata"

if (-not (Test-Path -PathType Container $targetDir)) {
    mkdir $targetDir
}

foreach ($f in $files) {
    $uri = $githubPrefix + $f
    $out = "$($targetDir)/$($f)"
    echo "Fetching '$($uri)' to '$($out)'"
    Invoke-WebRequest -Uri $uri -OutFile $out
}