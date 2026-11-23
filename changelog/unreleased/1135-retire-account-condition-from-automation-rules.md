---
type: Changed
---
Remove the Account condition from transaction automation rules: rules that used it lose that condition and stay disabled until you review them, and re-enabling one can now match more transactions than before (a rule left without conditions asks for confirmation and applies to all cash transactions). Such rules can be renamed or saved without conditions, and empty condition text is flagged on the field. #1135
