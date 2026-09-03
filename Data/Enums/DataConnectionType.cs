namespace AiHelpers.Data.Enums;

/// <summary>Only ODBC exists for now - an HTTP endpoint type is a deliberate stage-2 addition
/// (no standard auth/query shape across arbitrary APIs the way SQL-over-ODBC has one obvious
/// shape), not an oversight.</summary>
public enum DataConnectionType
{
    OdbcDatabase
}
