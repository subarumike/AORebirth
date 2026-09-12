using AORebirth.Database.Migrations;

return MigrationCommand.Run(args, () => Environment.GetEnvironmentVariable("AO_REBIRTH_MIGRATION_CONNECTION"), Console.Out);
