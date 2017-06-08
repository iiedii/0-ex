Zero-Example Video Search
=========================

This tool provides an efficient implementation for zero-example video search. It is derived from our successful system for *NIST TRECVID multimedia event detection (MED'15, '16)*, *ad-hoc video search (AVS'16)* and *MMM video browswer showdown (VBS'17)*. The tool is capable of both MED and AVS tasks, and also supports interactive search. The implementation has the state-of-the-art performance which can serve as a good baseline. We hope the open source can benefit future research.

We highlight the following features:

 - ***General-purpose, zero-example search***  \ Compatible for both simple queries and complex queries (event kits in MED).
 - ***High efficiency***  \ Support 10,000+ visual concepts and can finish a search within seconds on a laptop for a corpus size of around 300,000 videos/keyframes.
 - ***Interactive search***  \ Support human-in-the-loop. Human efforts can be involved into the concept screening which is an intermediate step where a user has chance to improve the search result while being kept away from directly seeing the result. Alternatively, a user can also perform interactive search by iteratively refining the result.
 - ***State-of-the-art performance and open source***  \ Can be used as a standalone tool or embedded as a module with ease.

The package encapsulates three datasets with deep net features and ground truth for benchmark. The datasets are ***(1) IACC.3 for AVS'16***, ***(2) MED14Test***, and ***(3) TV2008 search task***.

To get started, please follow this **[GUIDE](https://github.com/iiedii/0-ex/blob/master/Quick_Start.pdf)**. Have fun!


---------------------------------------------------------

**If you find this work helpful, please cite this work:**

```
@inproceedings{Lu2016Event,
 author = {Lu, Yi-Jie and Zhang, Hao and de Boer, Maaike and Ngo, Chong-Wah},
 title = {Event Detection with Zero Example: Select the Right and Suppress the Wrong Concepts},
 booktitle = {Proceedings of the 2016 ACM on International Conference on Multimedia Retrieval},
 series = {ICMR '16},
 year = {2016},
 isbn = {978-1-4503-4359-6},
 location = {New York, NY, USA},
 pages = {127--134},
 numpages = {8},
 url = {http://doi.acm.org/10.1145/2911996.2912015},
 doi = {10.1145/2911996.2912015},
 publisher = {ACM},
 keywords = {zero-example, concept bank, concept selection, multimedia event detection, semantic pooling, video search},
}
```

---------------------------------------------------------
