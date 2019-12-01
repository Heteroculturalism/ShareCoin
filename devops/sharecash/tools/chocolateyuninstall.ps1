write-output "Stopping ShareCash service..."
stop-service ShareCash
write-output "ShareCash service stopped."

write-output "Deleting ShareCash service..."
sc.exe delete ShareCash

stop-process -Name "Gui" -Force