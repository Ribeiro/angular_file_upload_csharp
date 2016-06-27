using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using AspNetWebApi.Extensions;
using AspNetWebApi.Models;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Net;

namespace AspNetWebApi.Controllers
{
    /// <summary>
    ///   This code is from a sample on MSDN  by Jay Chase
    ///   https://code.msdn.microsoft.com/AngularJS-with-Web-API-22f62a6e
    /// </summary>
    public class FilesController : ApiController
    {
        private readonly string workingFolder = HttpRuntime.AppDomainAppPath + @"\Uploads\";
        private const int MAX_FILE_SIZE = 1048576;
        private const string IMAGE_MIME_TYPE = "image";
        private const string TIPO_DE_MIDIA_NAO_SUPORTADO_MENSAGEM = "ERRO: Tipo de mídia não suportado!";
        private const string NENHUM_ARQUIVO_SELECIONADO_MENSAGEM = "ERRO: Nenhum arquivo foi selecionado!";
        private const string ARQUIVO_MAIOR_QUE_1_MB_MENSAGEM = "ERRO: O arquivo não pode ser maior que 1MB!";
        private const string ARQUIVO_NAO_EH_IMAGEM_MENSAGEM = "ERRO: O arquivo enviado não é uma imagem!";
        private const string UPLOAD_CONCLUIDO_COM_SUCESSO_MENSAGEM = "Upload concluído com sucesso!";
        private const string SERVIDOR_FALHOU_SOLICITACAO_MENSAGEM = "ERRO: O servidor falhou ao atender sua solicitação! ";

        /// <summary>
        ///   Get all photos
        /// </summary>
        /// <returns></returns>
        public async Task<IHttpActionResult> Get()
        {
            var photos = new List<PhotoViewModel>();

            var photoFolder = new DirectoryInfo(workingFolder);

            await Task.Factory.StartNew(() =>
            {
                photos = photoFolder.EnumerateFiles()
            .Where(fi => new[] { ".jpg", ".bmp", ".png", ".gif", ".tiff" }
              .Contains(fi.Extension.ToLower()))
            .Select(fi => new PhotoViewModel
            {
                Name = fi.Name,
                Created = fi.CreationTime,
                Modified = fi.LastWriteTime,
                Size = fi.Length / 1024
            })
            .ToList();
            });

            return Ok(new { Photos = photos });
        }

        /// <summary>
        ///   Delete photo
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        [HttpDelete]
        public async Task<IHttpActionResult> Delete(string fileName)
        {
            if (!FileExists(fileName))
            {
                return NotFound();
            }

            try
            {
                var filePath = Directory.GetFiles(workingFolder, fileName)
                  .FirstOrDefault();

                await Task.Factory.StartNew(() =>
                {
                    if (filePath != null)
                        File.Delete(filePath);
                });

                var result = new PhotoActionResult
                {
                    Successful = true,
                    Message = fileName + "deleted successfully"
                };
                return Ok(new { message = result.Message });
            }
            catch (Exception ex)
            {
                var result = new PhotoActionResult
                {
                    Successful = false,
                    Message = "error deleting fileName " + ex.GetBaseException().Message
                };
                return BadRequest(result.Message);
            }
        }

        /// <summary>
        ///   Add a photo
        /// </summary>
        /// <returns></returns>
        public async Task<HttpResponseMessage> Add()
        {
            if (!Request.Content.IsMimeMultipartContent())
            {
                return BuildResponseWith(HttpStatusCode.UnsupportedMediaType, TIPO_DE_MIDIA_NAO_SUPORTADO_MENSAGEM);
            }

            HttpFileCollection hfc = HttpContext.Current.Request.Files;
            if (hfc.Count == 0)
            {
                return BuildResponseWith(HttpStatusCode.BadRequest, NENHUM_ARQUIVO_SELECIONADO_MENSAGEM);
            }

            HttpPostedFile httpPostedFile = hfc[0];

            if (httpPostedFile.ContentLength > MAX_FILE_SIZE)
            {
                return BuildResponseWith(HttpStatusCode.BadRequest, ARQUIVO_MAIOR_QUE_1_MB_MENSAGEM);
            }

            if (!httpPostedFile.ContentType.Contains(IMAGE_MIME_TYPE))
            {
                return BuildResponseWith(HttpStatusCode.BadRequest, ARQUIVO_NAO_EH_IMAGEM_MENSAGEM);
            }

            Stream imageStream = null;
            Image originalImage = null;
            Image imageReadyToSave = null;

            try
            {
                imageStream = httpPostedFile.InputStream;
                originalImage = Image.FromStream(imageStream);
                imageReadyToSave = AdjustDimensionsOn(originalImage, 100);
                imageReadyToSave.Save(workingFolder + Path.GetFileName(httpPostedFile.FileName), ImageFormat.Jpeg);
            }
            catch (Exception ex)
            {
                return BuildResponseWith(HttpStatusCode.ExpectationFailed, SERVIDOR_FALHOU_SOLICITACAO_MENSAGEM + ex.Message);

            }
            finally
            {
                if (null != imageReadyToSave)
                {
                    imageReadyToSave.Dispose();
                }

                if (null != originalImage)
                {
                    originalImage.Dispose();
                }

                if (null != imageStream)
                {
                    imageStream.Dispose();
                }

            }

            return Request.CreateResponse(HttpStatusCode.OK, UPLOAD_CONCLUIDO_COM_SUCESSO_MENSAGEM);
        }

        private byte[] ReadFile(HttpPostedFile file)
        {
            byte[] data = new Byte[file.ContentLength];
            file.InputStream.Read(data, 0, file.ContentLength);
            return data;
        }

        /// <summary>
        ///   Check if file exists on disk
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public bool FileExists(string fileName)
        {
            var file = Directory.GetFiles(workingFolder, fileName).FirstOrDefault();

            return file != null;
        }

        private HttpResponseMessage BuildResponseWith(HttpStatusCode httpStatusCode, String message)
        {
            return new HttpResponseMessage(httpStatusCode)
            {
                Content = new StringContent(message)
            };

        }

        private Image AdjustDimensionsOn(Image image, int maxSideSize)
        {
            int newWidth;
            int newHeight;
            int oldWidth = image.Width;
            int oldHeight = image.Height;
            Bitmap newImage;

            if (maxSideSize != 1)
            {
                int maxSide = oldWidth >= oldHeight ? oldWidth : oldHeight;

                if (maxSide > maxSideSize)
                {
                    double coeficient = maxSideSize / (double)maxSide;
                    newWidth = Convert.ToInt32(coeficient * oldWidth);
                    newHeight = Convert.ToInt32(coeficient * oldHeight);
                }
                else
                {
                    newWidth = oldWidth;
                    newHeight = oldHeight;
                }

                newImage = new Bitmap(image, newWidth, newHeight);

            }
            else
            {
                newImage = new Bitmap(image, oldWidth, oldHeight);
                using (Graphics graphics = Graphics.FromImage(newImage))
                {
                    //set the resize quality modes to high quality
                    graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    //draw the image into the target bitmap
                    graphics.DrawImage(image, 0, 0, newImage.Width, newImage.Height);
                }

            }

            return newImage;

        }

    }

}