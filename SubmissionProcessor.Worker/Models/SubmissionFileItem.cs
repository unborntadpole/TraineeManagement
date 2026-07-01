using System.ComponentModel.DataAnnotations;

namespace SubmissionProcessor.Worker.Models;

public class SubmissionFile
{
    public long Id { get; set; }
    public string OriginalFileName { get; set; }
    public string GeneratedStorageName { get; set; }
    public string ContentType { get; set; }
    public long Size { get; set; }
    public string Checksum { get; set; }
    public string UploadedByUser { get; set; }
    public DateTime UploadedDate { get; set; }

    public long SubmissionId { get; set; }
    public Submission Submission{ get; set; }

    public SubmissionFile(SubmissionFileDTO submissionFile)
    {
        OriginalFileName = submissionFile.OriginalFileName;
        GeneratedStorageName = submissionFile.GeneratedStorageName;
        ContentType = submissionFile.ContentType;
        Size = submissionFile.Size;
        Checksum = submissionFile.Checksum;
        UploadedByUser = submissionFile.UploadedByUser;
        UploadedDate = DateTime.UtcNow;
        SubmissionId = submissionFile.SubmissionId;
    }
    public SubmissionFile() {}

}


public class SubmissionFileDTO
{
    public long Id { get; set; }
    
    public string OriginalFileName { get; set; }
    
    public string GeneratedStorageName { get; set; }
    
    public string ContentType { get; set; }
    
    [Required(ErrorMessage = "Size of the file")]
    public long Size { get; set; }
    
    [Required(ErrorMessage = "Checksum is reuired and in SHA-256 format")]
    public string Checksum { get; set; }
    
    [Required(ErrorMessage = "Identity of the user who uploaded the file is required.")]
    public string UploadedByUser { get; set; }
    public DateTime UploadedDate { get; set; }
    
    [Required(ErrorMessage = "SubmissionId of the submission is required to match file upload.")]
    public long SubmissionId { get; set; }

    public SubmissionFileDTO( SubmissionFile submissionFile)
    {
        Id = submissionFile.Id;
        OriginalFileName = submissionFile.OriginalFileName;
        GeneratedStorageName = submissionFile.GeneratedStorageName;
        ContentType = submissionFile.ContentType;
        Size = submissionFile.Size;
        Checksum = submissionFile.Checksum;
        UploadedByUser = submissionFile.UploadedByUser;
        UploadedDate = submissionFile.UploadedDate;
        SubmissionId = submissionFile.SubmissionId;
    }

    public SubmissionFileDTO () {}
}

public class PostFileResponse
{
    public long Id { get; set; }
    public string OriginalFileName { get; set; }
    public string GeneratedStorageName { get; set; }
    public long Size { get; set; }
    public string ContentType { get; set; }
    public string Checksum { get; set; }
    public string UploadedByUser { get; set; }
    public string TrackingId { get; set; }
    public DateTime UploadedDate { get; set; }
    public PostFileResponse( SubmissionFile submissionFile)
    {
        Id = submissionFile.Id;
        OriginalFileName = submissionFile.OriginalFileName;
        GeneratedStorageName = submissionFile.GeneratedStorageName;
        ContentType = submissionFile.ContentType;
        Size = submissionFile.Size;
        Checksum = submissionFile.Checksum;
        UploadedByUser = submissionFile.UploadedByUser;
        UploadedDate = submissionFile.UploadedDate;
    }
}


public class DownloadFileDTO
{
    public FileStream Stream { get; set; }

    public string ContentType { set; get; }

    public string FileName { get; set; }

    public DownloadFileDTO(FileStream stream, string contentType, string fileName)
    {
        Stream = stream;
        ContentType = contentType;
        FileName = fileName;
    }
}