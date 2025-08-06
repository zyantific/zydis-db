# Information

We use the `Zydis.Generator.Enums` project to generate enums that are used in the Zydis library and the Zydis generator. 
This allows us to maintain a single source of truth for these enums, ensuring consistency and reducing duplication.

Generation is split out from the `Zydis.Generator.Core` project to be able to control the source generator invocation 
order.

This is important, because the `System.Text.Json` source generator needs to run after the `Zydis.Generator.Enums` 
source generator.

Edit `shared_enums.json` to add new enums or modify existing ones.
