﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ElectionResults.API.ViewModels;
using ElectionResults.Core.Entities;
using ElectionResults.Core.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ElectionResults.API.Controllers
{
    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment webHostEnvironment;
        private readonly IArticleRepository _articleRepository;
        private readonly IElectionRepository _electionRepository;
        private readonly IAuthorsRepository _authorsRepository;
        private readonly IPicturesRepository _picturesRepository;

        public HomeController(
            IWebHostEnvironment hostEnvironment,
            IArticleRepository articleRepository,
            IElectionRepository electionRepository,
            IAuthorsRepository authorsRepository,
            IPicturesRepository picturesRepository)
        {
            webHostEnvironment = hostEnvironment;
            _articleRepository = articleRepository;
            _electionRepository = electionRepository;
            _authorsRepository = authorsRepository;
            _picturesRepository = picturesRepository;
        }

        public async Task<IActionResult> Index()
        {
            var feeds = await _articleRepository.GetAllArticles();
            return View(feeds.Value);
        }

        public async Task<IActionResult> New()
        {
            var newsFeedViewModel = await BuildEditNewsFeedViewModel();
            return View(newsFeedViewModel);
        }

        private async Task<NewsViewModel> BuildEditNewsFeedViewModel()
        {
            var newsFeedViewModel = new NewsViewModel();
            var elections = await _electionRepository.GetElections();
            newsFeedViewModel.Elections = new List<SelectListItem>();
            foreach (var election in elections.Value)
            {
                var electionGroup = new SelectListGroup { Name = election.ElectionName };
                foreach (var ballot in election.Ballots)
                {
                    newsFeedViewModel.Elections.Add(new SelectListItem(ballot.Name, ballot.BallotId.ToString())
                    {
                        Group = electionGroup
                    });
                }
            }
            newsFeedViewModel.Date = DateTime.Today;

            newsFeedViewModel.SelectedElectionId = null;
            var authors = await _authorsRepository.GetAuthors();
            newsFeedViewModel.Authors = authors.Select(a => new SelectListItem(a.Name, a.Id.ToString())).ToList();
            return newsFeedViewModel;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _articleRepository.Delete(new Article { Id = id });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var newsFeedViewModel = await BuildEditNewsFeedViewModel();
                var result = await _articleRepository.GetById(id);
                var news = result.Value;
                newsFeedViewModel.SelectedAuthorId = news.AuthorId;
                newsFeedViewModel.SelectedElectionId = news.ElectionId;
                newsFeedViewModel.Body = news.Body;
                newsFeedViewModel.NewsId = news.Id;
                newsFeedViewModel.Date = news.Timestamp;
                newsFeedViewModel.Title = news.Title;
                newsFeedViewModel.Link = news.Link;
                newsFeedViewModel.UploadedPictures = news.Pictures;
                return View("New", newsFeedViewModel);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> New(NewsViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var elections = await _electionRepository.GetElections();
                    var ballots = elections.Value.SelectMany(e => e.Ballots).ToList();
                    var selectedBallot = ballots
                        .FirstOrDefault(b => b.BallotId == model.SelectedElectionId);
                    if (selectedBallot == null)
                        selectedBallot = ballots.FirstOrDefault();
                    var pictures = new List<ArticlePicture>();
                    if (model.Pictures != null && model.Pictures.Count > 0)
                    {
                        var uniqueFileNames = UploadedFiles(model);
                        foreach (var fileName in uniqueFileNames)
                        {
                            pictures.Add(new ArticlePicture
                            {
                                Url = $"/upload/{fileName}"
                            });
                        }
                        await _picturesRepository.RemovePictures(model.NewsId);
                    }

                    var newsFeed = new Article
                    {
                        Link = model.Link,
                        Title = model.Title,
                        Body = model.Body,
                        BallotId = selectedBallot.BallotId,
                        ElectionId = selectedBallot.ElectionId,
                        Timestamp = model.Date,
                        AuthorId = model.SelectedAuthorId.GetValueOrDefault(),
                        Pictures = pictures
                    };
                    newsFeed.Id = model.NewsId;
                    await _articleRepository.AddArticle(newsFeed);
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            var newsFeedViewModel = await BuildEditNewsFeedViewModel();
            return View(newsFeedViewModel);
        }

        private List<string> UploadedFiles(NewsViewModel model)
        {
            var filenames = new List<string>();
            if (model.Pictures != null && model.Pictures.Count == 2)
            {
                foreach (var picture in model.Pictures)
                {
                    string uploadsFolder = Path.Combine(webHostEnvironment.WebRootPath, "upload");
                    var uniqueFileName = Guid.NewGuid() + "_" + picture.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        picture.CopyTo(fileStream);
                    }
                    filenames.Add(uniqueFileName);
                }
                return filenames;
            }
            throw new ArgumentException("Wrong number of pictures uploaded");
        }
    }
}