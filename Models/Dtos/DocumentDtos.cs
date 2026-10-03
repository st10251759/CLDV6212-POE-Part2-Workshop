namespace PageTurn.Functions.Models.Dtos;

public record DocumentInfo(
    string FileName, long SizeInBytes, string ContentType,
    DateTimeOffset? UploadedAtUtc, DateTimeOffset? LastModified);

public record DocumentDownload(Stream Content, string ContentType, string FileName);