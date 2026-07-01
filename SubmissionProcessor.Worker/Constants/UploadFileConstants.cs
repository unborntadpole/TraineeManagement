namespace SubmissionProcessor.Worker.Constants;

public class UploadFilesConstants
{
    public static List<string> AllowedExtensions = [".txt", ".pdf"];

    // public static string UploadDirectory = "../SubmissionFiles/UploadedFiles";
    public static string UploadDirectory = "/App/uploads";

    // public static string RequestPath = "../SubmissionFiles/UploadedFiles";
    public static string RequestPath = "/App/uploads";

    // public static string RequestPath = "";

    public static long MaxLength =  20_971_520; // this is 20 mb

    public static double SubmissionProcessingRequestedVersion = 1.0;

}