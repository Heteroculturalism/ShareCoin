# $sourceDir = "ShareCoin"
# $repoUrl = "https://github.com/CashShareCoin/$sourceDir.git"

# # exit if directory already exists
# if ($(test-path $sourceDir -pathtype container) -eq $true)
# {
	# write-error "Cannot clone repo, folder '$sourceDir' already exists."
	# exit 1
# }

# # clone repo
# git clone $repoUrl
 
# cd $sourceDir

# start ShareCoin.sln
