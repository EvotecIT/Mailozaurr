Describe 'New-GraphMailboxPermissionObject builder parameter' {
    It 'Builds permission from builder' {
        $b = New-GraphMailboxPermissionBuilder -GrantedToUser 'a@example.com' -Roles Owner
        $perm = New-GraphMailboxPermissionObject -Builder $b
        $perm.GrantedTo.User | Should -Be 'a@example.com'
        $perm.Roles | Should -Contain 'Owner'
    }
}
