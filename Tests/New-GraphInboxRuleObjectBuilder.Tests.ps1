Describe 'New-GraphInboxRuleObject builder parameter' {
    It 'Builds rule from builder' {
        $b = New-GraphInboxRuleBuilder -DisplayName 'Test' -Sequence 1 -SenderContains 'a@example.com'
        $rule = New-GraphInboxRuleObject -Builder $b
        $rule.DisplayName | Should -Be 'Test'
        $rule.Sequence | Should -Be 1
        $rule.Conditions.SenderContains | Should -Contain 'a@example.com'
    }
}
