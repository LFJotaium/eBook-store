--
-- PostgreSQL database dump
--

-- Dumped from database version 14.15 (Homebrew)
-- Dumped by pg_dump version 14.15 (Homebrew)

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: Users; Type: TABLE; Schema: public; Owner: ebookstore_user
--

CREATE TABLE public."Users" (
    "Username" text NOT NULL,
    "FirstName" character varying(50) NOT NULL,
    "LastName" character varying(50) NOT NULL,
    "Email" text NOT NULL,
    "Password" text NOT NULL,
    "Role" text DEFAULT 'User'::text NOT NULL
);


ALTER TABLE public."Users" OWNER TO ebookstore_user;

--
-- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: ebookstore_user
--

CREATE TABLE public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL
);


ALTER TABLE public."__EFMigrationsHistory" OWNER TO ebookstore_user;

--
-- Name: books; Type: TABLE; Schema: public; Owner: ebookstore_user
--

CREATE TABLE public.books (
    id integer NOT NULL,
    title character varying(255) NOT NULL,
    authorname character varying(255) NOT NULL,
    publisher character varying(255) NOT NULL,
    pricebuy numeric(10,2) NOT NULL,
    priceborrowing numeric(10,2) NOT NULL,
    yearofpublish integer NOT NULL,
    genre character varying(255),
    coverimagepath character varying(255)
);


ALTER TABLE public.books OWNER TO ebookstore_user;

--
-- Name: books_id_seq; Type: SEQUENCE; Schema: public; Owner: ebookstore_user
--

CREATE SEQUENCE public.books_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER TABLE public.books_id_seq OWNER TO ebookstore_user;

--
-- Name: books_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: ebookstore_user
--

ALTER SEQUENCE public.books_id_seq OWNED BY public.books.id;


--
-- Name: borrowedbooks; Type: TABLE; Schema: public; Owner: ebookstore_user
--

CREATE TABLE public.borrowedbooks (
    id integer NOT NULL,
    bookid integer NOT NULL,
    username character varying(255) NOT NULL,
    borrowdate timestamp without time zone NOT NULL,
    returndate timestamp without time zone
);


ALTER TABLE public.borrowedbooks OWNER TO ebookstore_user;

--
-- Name: prices; Type: TABLE; Schema: public; Owner: ebookstore_user
--

CREATE TABLE public.prices (
    id integer NOT NULL,
    bookid integer,
    currentpricebuy numeric(10,2) NOT NULL,
    currentpriceborrow numeric(10,2) NOT NULL,
    originalpricebuy numeric(10,2) NOT NULL,
    originalpriceborrow numeric(10,2) NOT NULL,
    isdiscounted boolean DEFAULT false,
    discountenddate timestamp without time zone
);


ALTER TABLE public.prices OWNER TO ebookstore_user;

--
-- Name: prices_id_seq; Type: SEQUENCE; Schema: public; Owner: ebookstore_user
--

CREATE SEQUENCE public.prices_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER TABLE public.prices_id_seq OWNER TO ebookstore_user;

--
-- Name: prices_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: ebookstore_user
--

ALTER SEQUENCE public.prices_id_seq OWNED BY public.prices.id;


--
-- Name: purchasedbooks; Type: TABLE; Schema: public; Owner: ebookstore_user
--

CREATE TABLE public.purchasedbooks (
    id integer NOT NULL,
    bookid integer NOT NULL,
    username character varying(255) NOT NULL,
    purchasedate timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.purchasedbooks OWNER TO ebookstore_user;

--
-- Name: users; Type: TABLE; Schema: public; Owner: ebookstore_user
--

CREATE TABLE public.users (
    username character varying(50) NOT NULL,
    firstname character varying(50),
    lastname character varying(50),
    email character varying(100),
    password character varying(100),
    role character varying(50)
);


ALTER TABLE public.users OWNER TO ebookstore_user;

--
-- Name: books id; Type: DEFAULT; Schema: public; Owner: ebookstore_user
--

ALTER TABLE ONLY public.books ALTER COLUMN id SET DEFAULT nextval('public.books_id_seq'::regclass);


--
-- Name: prices id; Type: DEFAULT; Schema: public; Owner: ebookstore_user
--

ALTER TABLE ONLY public.prices ALTER COLUMN id SET DEFAULT nextval('public.prices_id_seq'::regclass);


--
-- Data for Name: Users; Type: TABLE DATA; Schema: public; Owner: ebookstore_user
--

COPY public."Users" ("Username", "FirstName", "LastName", "Email", "Password", "Role") FROM stdin;
johndoe	John	Doe	john.doe@example.com	Passw0rd123	Admin
\.


--
-- Data for Name: __EFMigrationsHistory; Type: TABLE DATA; Schema: public; Owner: ebookstore_user
--

COPY public."__EFMigrationsHistory" ("MigrationId", "ProductVersion") FROM stdin;
20241214205319_CreateUsersTable	9.0.0
20241214211017_InitialCreate	9.0.0
\.


--
-- Data for Name: books; Type: TABLE DATA; Schema: public; Owner: ebookstore_user
--

COPY public.books (id, title, authorname, publisher, pricebuy, priceborrowing, yearofpublish, genre, coverimagepath) FROM stdin;
2	To Kill a Mockingbird	Harper Lee	J.B. Lippincott & Co.	12.99	3.49	1960	Fiction	https://media.glamour.com/photos/56e1f3c4bebf143c52613c00/master/w_960,c_limit/entertainment-2016-02-06-main.jpg
4	The Road	Cormac McCarthy	Knopf	14.99	4.49	2006	Fiction	https://target.scene7.com/is/image/Target/GUEST_d7d0f623-e032-4187-85e4-189f06607d42?wid=600&hei=600&qlt=80&fmt=webp
5	The Help	Kathryn Stockett	Putnam	13.49	3.99	2009	Historical Fiction	https://www.bookxcess.com/cdn/shop/products/9780141039282_1_576x.jpg?v=1676605003
6	Life of Pi	Yann Martel	Knopf	12.99	3.49	2001	Adventure	https://cgassets-1d48b.kxcdn.com/site/assets/files/438317/getimage.jpg
7	A Thousand Splendid Suns	Khaled Hosseini	Riverhead Books	14.99	4.49	2007	Historical Fiction	https://res.cloudinary.com/bloomsbury-atlas/image/upload/w_360,c_scale,dpr_1.5/jackets/9781526604750.jpg
8	The Fault in Our Stars	John Green	Dutton Books	14.99	3.99	2012	Young Adult	https://images-na.ssl-images-amazon.com/images/S/compressed.photo.goodreads.com/books/1660273739i/11870085.jpg
9	The Night Circus	Erin Morgenstern	Doubleday	15.99	4.49	2011	Fantasy	https://erinmorgenstern.com/wp-content/uploads/2011/03/Night-Circus-Cover-low-res.jpg
10	The Book Thief	Markus Zusak	Knopf	13.49	3.49	2005	Historical Fiction	https://www.booknet.co.il/Images/Site/Products/org/9781909531611.jpg
11	Educated	Tara Westover	Random House	17.99	4.99	2018	Memoir	https://www.steimatzky.co.il/pub/media/catalog/product/cache/054fd023ed4beb824f3143faa6fcc008/0/2/020074927-1689232102155699.jpg
12	Circe	Madeline Miller	Little, Brown and Company	15.49	4.49	2018	Fantasy	https://www.washingtonpost.com/wp-apps/imrs.php?src=https://arc-anglerfish-washpost-prod-washpost.s3.amazonaws.com/public/24FPALQQIEI6RFLQFHEYGBJV4U.jpg&w=210
13	The Silent Patient	Alex Michaelides	Celadon Books	16.99	4.99	2019	Thriller	https://www.booknet.co.il/Images/Site/Products/9781409181637.jpg
14	Normal People	Sally Rooney	Hogarth Press	14.99	3.99	2018	Fiction	https://images-na.ssl-images-amazon.com/images/S/compressed.photo.goodreads.com/books/1571423190i/41057294.jpg
16	The Vanishing Half	Brit Bennett	Riverhead Books	10.99	3.99	2020	Historical fiction	https://www.sipurpashut.com/cdn/shop/products/51yL5wdoHfL._SX322_BO1_204_203_200_300x.jpg?v=1611506450
1	The Great Gatsby	F. Scott Fitzgerald	Scribner	10.99	2.99	1925	Fiction	https://i0.wp.com/americanwritersmuseum.org/wp-content/uploads/2018/02/CK-3.jpg?resize=267%2C400&ssl=1
17	The Light We Lost	Jill Santopolo	G.P. Putnam's Sons	14.99	3.99	2017	Fiction	https://images-na.ssl-images-amazon.com/images/S/compressed.photo.goodreads.com/books/1493724414i/32956365.jpg
18	The Nightingale	Kristin Hannah	St. Martin's Press	16.99	4.49	2015	Historical Fiction	https://www.libertybooks.com/image/cache/catalog/9781509848621-313x487.jpg?q6
19	The Girl on the Train	Paula Hawkins	Riverhead Books	15.99	4.49	2015	Thriller	https://cdn2.penguin.com.au/covers/original/9781784161750.jpg
20	Big Little Lies	Liane Moriarty	Penguin Books	14.99	3.99	2014	Fiction	https://i1.wp.com/abelleinabookshop.com/wp-content/uploads/2017/05/Screen-Shot-2017-05-15-at-11.04.00-AM.png?w=288&ssl=1
21	The Overstory	Richard Powers	W.W. Norton & Company	17.99	4.99	2018	Fiction	https://m.media-amazon.com/images/I/61VCVNtbg5L._SL500_.jpg
22	The Song of Achilles	Madeline Miller	Ecco Press	15.49	4.49	2011	Historical Fiction	https://target.scene7.com/is/image/Target/GUEST_ef088868-93cf-403d-8bf5-7391d5fdc3fb?wid=600&hei=600&qlt=80&fmt=webp
23	The Testaments	Margaret Atwood	Nan A. Talese	18.99	5.49	2019	Dystopian	https://the-bibliofile.com/wp-content/uploads/testaments2.png
24	Homegoing	Yaa Gyasi	Knopf	16.99	4.49	2016	Historical Fiction	https://www.washingtonpost.com/wp-apps/imrs.php?src=https://arc-anglerfish-washpost-prod-washpost.s3.amazonaws.com/public/FMQCIARCTAI6NKUEII4RXJJMSE&w=1200
25	Red Rising	Pierce Brown	Del Rey	14.99	3.99	2014	Science Fiction	https://target.scene7.com/is/image/Target/GUEST_8eb223bb-af32-4f03-a768-6887a9391e9f?wid=600&hei=600&qlt=80&fmt=webp
27	The Hunger Games	Suzanne Collins	Scholastic Press	12.90	3.50	2008	Dystopian	https://static-ppimages.freetls.fastly.net/nielsens/9781407132082.jpg?canvas=600,600&fit=bounds&height=600&mode=max&width=600&404=default.jpg
15	21 Lessons for the 21st Century	Yuval Noah Harari	Spiegel & Grau	17.00	5.50	2018	Social philosophy	https://thebookshopccs.com/cdn/shop/products/21LESSONS.png?v=1680370509&width=1000
\.


--
-- Data for Name: borrowedbooks; Type: TABLE DATA; Schema: public; Owner: ebookstore_user
--

COPY public.borrowedbooks (id, bookid, username, borrowdate, returndate) FROM stdin;
\.


--
-- Data for Name: prices; Type: TABLE DATA; Schema: public; Owner: ebookstore_user
--

COPY public.prices (id, bookid, currentpricebuy, currentpriceborrow, originalpricebuy, originalpriceborrow, isdiscounted, discountenddate) FROM stdin;
1	2	12.99	3.49	12.99	3.49	f	\N
2	4	14.99	4.49	14.99	4.49	f	\N
3	5	13.49	3.99	13.49	3.99	f	\N
4	6	12.99	3.49	12.99	3.49	f	\N
5	7	14.99	4.49	14.99	4.49	f	\N
6	8	14.99	3.99	14.99	3.99	f	\N
7	9	15.99	4.49	15.99	4.49	f	\N
8	10	13.49	3.49	13.49	3.49	f	\N
9	11	17.99	4.99	17.99	4.99	f	\N
10	12	15.49	4.49	15.49	4.49	f	\N
11	13	16.99	4.99	16.99	4.99	f	\N
12	14	14.99	3.99	14.99	3.99	f	\N
14	16	10.99	3.99	10.99	3.99	f	\N
15	1	10.99	2.99	10.99	2.99	f	\N
16	17	14.99	3.99	14.99	3.99	f	\N
17	18	16.99	4.49	16.99	4.49	f	\N
18	19	15.99	4.49	15.99	4.49	f	\N
19	20	14.99	3.99	14.99	3.99	f	\N
20	21	17.99	4.99	17.99	4.99	f	\N
21	22	15.49	4.49	15.49	4.49	f	\N
22	23	18.99	5.49	18.99	5.49	f	\N
23	24	16.99	4.49	16.99	4.49	f	\N
24	25	14.99	3.99	14.99	3.99	f	\N
27	27	11.50	3.00	12.90	3.50	t	2025-01-02 00:00:00
13	15	17.00	5.50	16.99	5.00	f	\N
\.


--
-- Data for Name: purchasedbooks; Type: TABLE DATA; Schema: public; Owner: ebookstore_user
--

COPY public.purchasedbooks (id, bookid, username, purchasedate) FROM stdin;
\.


--
-- Data for Name: users; Type: TABLE DATA; Schema: public; Owner: ebookstore_user
--

COPY public.users (username, firstname, lastname, email, password, role) FROM stdin;
johndoe	John	Doe	john.doe@example.com	+ocKJlj8SZk1efdHHYbQpUyhJn5S0/m+I5XW82ib3Mc=	Admin
fadiQ	fadi	Que	fadi.doe@example.com	+ocKJlj8SZk1efdHHYbQpUyhJn5S0/m+I5XW82ib3Mc=	User
firasa	firas	abu	firas@gmail.com	HQdi/rmy6oHP6UvBMqitjV6mwJeMTIpjL5ro2ylXdCA=	User
samer	samer	ataria	samer@gmail.com	w1hL5dY4oBs8xHVziHNF44mHHzuOpAnYQQyLLPkx5Pw=	User
test1	testt	BB	test@hotmail.com	HQdi/rmy6oHP6UvBMqitjV6mwJeMTIpjL5ro2ylXdCA=	User
test2	testt	BB	test@gmail.com	HQdi/rmy6oHP6UvBMqitjV6mwJeMTIpjL5ro2ylXdCA=	User
aas	asa	no	no@gmail.com	HQdi/rmy6oHP6UvBMqitjV6mwJeMTIpjL5ro2ylXdCA=	User
recently	firas	abu	nao@gmail.com	HQdi/rmy6oHP6UvBMqitjV6mwJeMTIpjL5ro2ylXdCA=	User
newt	new	test	newt@gmail.com	HQdi/rmy6oHP6UvBMqitjV6mwJeMTIpjL5ro2ylXdCA=	User
\.


--
-- Name: books_id_seq; Type: SEQUENCE SET; Schema: public; Owner: ebookstore_user
--

SELECT pg_catalog.setval('public.books_id_seq', 27, true);


--
-- Name: prices_id_seq; Type: SEQUENCE SET; Schema: public; Owner: ebookstore_user
--

SELECT pg_catalog.setval('public.prices_id_seq', 27, true);


--
-- Name: Users PK_Users; Type: CONSTRAINT; Schema: public; Owner: ebookstore_user
--

ALTER TABLE ONLY public."Users"
    ADD CONSTRAINT "PK_Users" PRIMARY KEY ("Username");


--
-- Name: __EFMigrationsHistory PK___EFMigrationsHistory; Type: CONSTRAINT; Schema: public; Owner: ebookstore_user
--

ALTER TABLE ONLY public."__EFMigrationsHistory"
    ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");


--
-- Name: books books_pkey; Type: CONSTRAINT; Schema: public; Owner: ebookstore_user
--

ALTER TABLE ONLY public.books
    ADD CONSTRAINT books_pkey PRIMARY KEY (id);


--
-- Name: prices prices_pkey; Type: CONSTRAINT; Schema: public; Owner: ebookstore_user
--

ALTER TABLE ONLY public.prices
    ADD CONSTRAINT prices_pkey PRIMARY KEY (id);


--
-- Name: users users_pkey; Type: CONSTRAINT; Schema: public; Owner: ebookstore_user
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_pkey PRIMARY KEY (username);


--
-- Name: IX_Users_Email; Type: INDEX; Schema: public; Owner: ebookstore_user
--

CREATE UNIQUE INDEX "IX_Users_Email" ON public."Users" USING btree ("Email");


--
-- Name: prices prices_bookid_fkey; Type: FK CONSTRAINT; Schema: public; Owner: ebookstore_user
--

ALTER TABLE ONLY public.prices
    ADD CONSTRAINT prices_bookid_fkey FOREIGN KEY (bookid) REFERENCES public.books(id) ON DELETE CASCADE;


--
-- PostgreSQL database dump complete
--

