-- phpMyAdmin SQL Dump
-- version 5.2.0
-- https://www.phpmyadmin.net/
--
-- Host: ffxiv_db
-- Generation Time: Jul 13, 2022 at 10:59 AM
-- Server version: 10.8.3-MariaDB-1:10.8.3+maria~jammy
-- PHP Version: 8.0.19

SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
START TRANSACTION;
SET time_zone = "+00:00";


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;

--
-- Database: `ffxiv_db`
--

-- --------------------------------------------------------

--
-- Table structure for table `worlds`
--

CREATE TABLE `worlds` (
  `id` int(11) NOT NULL,
  `server` varchar(45) NOT NULL,
  `name` varchar(45) NOT NULL,
  `region` varchar(45) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

--
-- Dumping data for table `worlds`
--

INSERT INTO `worlds` (`id`, `server`, `name`, `region`) VALUES
(1, 'Chaos', 'Cerberus', 'Europe'),
(2, 'Chaos', 'Louisoix', 'Europe'),
(3, 'Chaos', 'Moogle', 'Europe'),
(4, 'Chaos', 'Omega', 'Europe'),
(5, 'Chaos', 'Phantom', 'Europe'),
(6, 'Chaos', 'Ragnarok', 'Europe'),
(7, 'Chaos', 'Sagittarius', 'Europe'),
(8, 'Chaos', 'Spriggan', 'Europe'),
(9, 'Light', 'Alpha', 'Europe'),
(10, 'Light', 'Lich', 'Europe'),
(11, 'Light', 'Odin', 'Europe'),
(12, 'Light', 'Phoenix', 'Europe'),
(13, 'Light', 'Raiden', 'Europe'),
(14, 'Light', 'Shiva', 'Europe'),
(15, 'Light', 'Twintania', 'Europe'),
(16, 'Light', 'Zodiark', 'Europe');

--
-- Indexes for dumped tables
--

--
-- Indexes for table `worlds`
--
ALTER TABLE `worlds`
  ADD PRIMARY KEY (`id`);

--
-- AUTO_INCREMENT for dumped tables
--

--
-- AUTO_INCREMENT for table `worlds`
--
ALTER TABLE `worlds`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=17;
COMMIT;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
