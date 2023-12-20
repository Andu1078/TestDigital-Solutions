using Microsoft.AspNetCore.Mvc;
using TestDigital_Solutions.Models;
using Microsoft.Extensions.Hosting;
using OpenCvSharp;
using System;
using System.IO;
using System.Threading.Tasks;

namespace TestDigital_Solutions.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHostEnvironment _hostEnvironment;

        public HomeController(IHostEnvironment hostEnvironment)
        {
            _hostEnvironment = hostEnvironment;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ProcesarVideo(IFormFile videoFile)
        {
            if (videoFile == null || videoFile.Length == 0)
            {
                return RedirectToAction("Index");
            }

            string carpetaFrames = Path.Combine(_hostEnvironment.ContentRootPath, "Frames");
            string carpetaFramesDiferencias = Path.Combine(_hostEnvironment.ContentRootPath, "FramesDiferencias");
            Directory.CreateDirectory(carpetaFrames);
            Directory.CreateDirectory(carpetaFramesDiferencias);

            string rutaVideo = Path.Combine(carpetaFrames, videoFile.FileName);

            using (var fileStream = new FileStream(rutaVideo, FileMode.Create))
            {
                await videoFile.CopyToAsync(fileStream);
            }

            ProcesarVideo(rutaVideo, carpetaFrames);
            ProcesarDiferenciasFaciales(carpetaFrames, carpetaFramesDiferencias);

        

            var viewModel = new ImagenesViewModel
            {
                ImagenOriginalPath = ObtenerRutaImagenOriginal(),
                ImagenProcesadaPath = ObtenerRutaImagenProcesada()
            };


            return RedirectToAction("MostrarImagenes", viewModel);
        }

        private void ProcesarVideo(string rutaVideo, string carpetaFrames)
        {
            const int cantidadFramesDeseados = 20;

            using (var capture = new VideoCapture(rutaVideo))
            using (var faceCascade = new CascadeClassifier("Data/haarcascade_frontalface_default.xml"))
            {
                Mat frame = new Mat();
                int framesProcesados = 0;
                while (capture.Read(frame) && framesProcesados < cantidadFramesDeseados)
                {
                    // Guardar el frame en la carpeta
                    string nombreFrame = $"{carpetaFrames}/frame_{capture.PosFrames}.jpg";
                    Cv2.ImWrite(nombreFrame, frame);
                    framesProcesados++;
                }
            }
        }


        private void ProcesarDiferenciasFaciales(string carpetaFrames, string carpetaFramesDiferencias)
        {
            var faceCascade = new CascadeClassifier("Data/haarcascade_frontalface_default.xml");
            var eyesCascade = new CascadeClassifier("Data/haarcascade_eye.xml");

            var frames = Directory.GetFiles(carpetaFrames, "frame_*.jpg");

            foreach (var framePath in frames)
            {
                Mat frame = Cv2.ImRead(framePath);

                // Convertir a escala de grises para la detección
                var gray = new Mat();
                Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);

                // Detectar rostros
                var rostros = faceCascade.DetectMultiScale(gray, 1.1, 3, HaarDetectionTypes.ScaleImage, new Size(30, 30));

                foreach (var rostro in rostros)
                {
                    // Dibujar un rectángulo alrededor del rostro
                    Cv2.Rectangle(frame, rostro, Scalar.Red, 2);

                    // Detección de ojos en la región del rostro
                    var roiGray = gray[rostro];
                    var ojos = eyesCascade.DetectMultiScale(roiGray);

                    foreach (var ojo in ojos)
                    {
                        // Dibujar un rectángulo alrededor de cada ojo
                        var ojoRect = new Rect(rostro.X + ojo.X, rostro.Y + ojo.Y, ojo.Width, ojo.Height);
                        Cv2.Rectangle(frame, ojoRect, Scalar.Green, 2);
                    }
                }

                // Guardar el frame con diferencias faciales resaltadas en la carpeta "frames_diferencias"
                string nombreFrameConDiferencias = $"{carpetaFramesDiferencias}/frame_con_diferencias_{Path.GetFileName(framePath)}";
                Cv2.ImWrite(nombreFrameConDiferencias, frame);
            }
        }

        private string ObtenerRutaImagenOriginal()
        {
            string carpetaFrames = Path.Combine(_hostEnvironment.ContentRootPath, "Frames");
            string rutaImagenOriginal = Path.Combine(carpetaFrames, "frame_1.jpg").Replace("\\", "/");
            return rutaImagenOriginal;
        }

        private string ObtenerRutaImagenProcesada()
        {
            string carpetaFramesDiferencias = Path.Combine(_hostEnvironment.ContentRootPath, "FramesDiferencias");
            string rutaImagenProcesada = Path.Combine(carpetaFramesDiferencias, "frame_con_diferencias_1.jpg").Replace("\\", "/");
            return rutaImagenProcesada;
        }


        public IActionResult MostrarImagenes(string imagenOriginalPath, string imagenProcesadaPath)
        {
            var model = new ImagenesViewModel(); // Reemplaza 'TuModelo' con el tipo de tu modelo
            model.ImagenOriginalPath = imagenOriginalPath;
            model.ImagenProcesadaPath = imagenProcesadaPath;

            return View(model);
        }

        public IActionResult BorrarImagenes()
        {
            BorrarArchivosEnCarpeta("Frames");
            BorrarArchivosEnCarpeta("FramesDiferencias");

            return RedirectToAction("Index");
        }

        private void BorrarArchivosEnCarpeta(string carpeta)
        {
            var archivos = Directory.GetFiles(Path.Combine(_hostEnvironment.ContentRootPath, carpeta));

            foreach (var archivo in archivos)
            {
                System.IO.File.Delete(archivo);
            }
        }


    }
}